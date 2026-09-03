//! Port of `Webhooks/Outbound/OutboundUrl.cs` — SSRF defense for outbound
//! webhooks: private/loopback range checks at registration, per-attempt DNS
//! re-resolution, and (issue 017) connect-time pinning via a ureq `Resolver`
//! so the dialed IP is the validated answer, not a post-check re-resolution.

use std::net::{IpAddr, SocketAddr, TcpStream, ToSocketAddrs};

#[derive(Debug, thiserror::Error)]
pub enum OutboundUrlError {
    #[error("url is not allowed")]
    NotAllowed,
    #[error("no allowed address to dial")]
    NoAllowedAddress,
    #[error("connect failed: {0}")]
    Connect(String),
}

pub fn is_loopback(ip: IpAddr) -> bool {
    ip.is_loopback()
}

/// Private, loopback, link-local, and the cloud metadata ranges.
pub fn is_private_or_loopback(ip: IpAddr) -> bool {
    match ip {
        IpAddr::V4(v4) => {
            v4.is_private()
                || v4.is_loopback()
                || v4.is_link_local()
                || v4.is_unspecified()
                || v4.is_broadcast()
                // 169.254.169.254 sits in link-local; 100.64/10 carrier NAT also blocked.
                || (v4.octets()[0] == 100 && (v4.octets()[1] & 0xC0) == 64)
        }
        IpAddr::V6(v6) => v6.is_loopback() || v6.is_unspecified() || (v6.segments()[0] & 0xffc0) == 0xfc00,
    }
}

pub fn allows_loopback(environment: &str) -> bool {
    environment == "Development" || environment == "Testing"
}

pub fn is_disallowed(ip: IpAddr, environment: &str) -> bool {
    is_private_or_loopback(ip) && !allows_loopback(environment)
}

/// Resolve a host:port and keep only allowed addresses. Empty when nothing
/// survives the filter — the caller dead-rows or fails the dial.
pub fn resolve_allowed(host: &str, port: u16, environment: &str) -> Vec<SocketAddr> {
    let Ok(addrs) = (host, port).to_socket_addrs() else {
        return vec![];
    };
    let mut resolved: Vec<SocketAddr> = addrs.collect();
    resolved.retain(|addr| !is_disallowed(addr.ip(), environment));
    resolved
}

/// ureq `Resolver` that pins the dial to a validated address — issue 017's
/// connect-time check. ureq calls this per attempt, so DNS re-resolution
/// happens every send and every answer is filtered before the dial.
pub struct PinningResolver {
    pub environment: String,
}

impl ureq::Resolver for PinningResolver {
    fn resolve(&self, netloc: &str) -> std::io::Result<Vec<SocketAddr>> {
        let (host, port) = split_netloc(netloc)?;
        let literal: Option<IpAddr> = host.parse().ok();
        if let Some(ip) = literal {
            if is_disallowed(ip, &self.environment) {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::PermissionDenied,
                    "url resolves to a private address",
                ));
            }
            return Ok(vec![SocketAddr::new(ip, port)]);
        }
        let allowed = resolve_allowed(&host, port, &self.environment);
        if allowed.is_empty() {
            return Err(std::io::Error::new(
                std::io::ErrorKind::PermissionDenied,
                "url resolves to a private address",
            ));
        }
        Ok(allowed)
    }
}

fn split_netloc(netloc: &str) -> std::io::Result<(&str, u16)> {
    let (host, port_str) = netloc
        .rsplit_once(':')
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::InvalidInput, "no port"))?;
    let port: u16 = port_str.parse().map_err(|_| {
        std::io::Error::new(std::io::ErrorKind::InvalidInput, "bad port")
    })?;
    Ok((host.trim_matches(|c| c == '[' || c == ']'), port))
}

/// Direct dial to a validated address (used by tests to prove the pin works at
/// the socket level: loopback refused in Production mode, dialable in Testing).
pub fn connect_validated(host: &str, port: u16, environment: &str) -> Result<TcpStream, OutboundUrlError> {
    let allowed = resolve_allowed(host, port, environment);
    let first = allowed.first().ok_or(OutboundUrlError::NoAllowedAddress)?;
    TcpStream::connect(first).map_err(|e| OutboundUrlError::Connect(e.to_string()))
}
