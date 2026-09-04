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
/// IPv6 matches C# `OutboundUrl.IsPrivateOrLoopback` (unique-local fc00::/7,
/// link-local fe80::/10, multicast ff00::/8, v4-mapped unwrap).
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
        IpAddr::V6(v6) => {
            let b = v6.octets();
            // ::ffff:a.b.c.d — judge the embedded IPv4, not the wrapper.
            if b[..10].iter().all(|&x| x == 0) && b[10] == 0xff && b[11] == 0xff {
                return is_private_or_loopback(IpAddr::V4(std::net::Ipv4Addr::new(
                    b[12], b[13], b[14], b[15],
                )));
            }
            v6.is_loopback()
                || v6.is_unspecified()
                || v6.is_multicast()
                || (b[0] == 0xfe && (b[1] & 0xc0) == 0x80)
                || (b[0] & 0xfe) == 0xfc
        }
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ipv6_unique_local_link_local_and_mapped_private_are_blocked() {
        let fd: IpAddr = "fd12:3456:789a::1".parse().unwrap();
        assert!(is_private_or_loopback(fd), "unique-local fd00::/8");
        let fe80: IpAddr = "fe80::1".parse().unwrap();
        assert!(is_private_or_loopback(fe80), "link-local fe80::/10");
        let mapped: IpAddr = "::ffff:192.168.1.1".parse().unwrap();
        assert!(is_private_or_loopback(mapped), "v4-mapped RFC1918");
        let public: IpAddr = "2001:4860:4860::8888".parse().unwrap();
        assert!(!is_private_or_loopback(public), "public unicast");
    }
}
