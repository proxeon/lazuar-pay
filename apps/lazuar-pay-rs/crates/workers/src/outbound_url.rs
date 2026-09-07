//! Port of `OutboundUrl.cs` private-range checks (issue 017 / 028 P1-15).

use std::net::{IpAddr, Ipv4Addr, Ipv6Addr, ToSocketAddrs};

pub fn allows_loopback(testing_or_dev: bool) -> bool {
    testing_or_dev
}

pub fn is_loopback_ip(ip: IpAddr) -> bool {
    ip.is_loopback()
}

pub fn is_disallowed(ip: IpAddr, allow_loopback: bool) -> bool {
    is_private_or_loopback(ip) && !(allow_loopback && is_loopback_ip(ip))
}

pub fn is_private_or_loopback(ip: IpAddr) -> bool {
    match ip {
        IpAddr::V4(v4) => is_private_v4(v4),
        IpAddr::V6(v6) => is_private_v6(v6),
    }
}

fn is_private_v4(ip: Ipv4Addr) -> bool {
    let b = ip.octets();
    b[0] == 0
        || b[0] == 10
        || b[0] == 127
        || b[0] >= 224
        || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
        || (b[0] == 169 && b[1] == 254)
        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)
        || (b[0] == 198 && b[1] >= 18 && b[1] <= 19)
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)
}

fn is_private_v6(ip: Ipv6Addr) -> bool {
    if ip.is_loopback() {
        return true;
    }
    let b = ip.octets();
    if b.len() == 16 && b[..10].iter().all(|&x| x == 0) && b[10] == 0xFF && b[11] == 0xFF {
        return is_private_v4(Ipv4Addr::new(b[12], b[13], b[14], b[15]));
    }
    if b.iter().take(12).all(|&x| x == 0) && b.iter().any(|&x| x != 0) {
        return is_private_v4(Ipv4Addr::new(b[12], b[13], b[14], b[15]));
    }
    if b[0] == 0x00
        && b[1] == 0x64
        && b[2] == 0xFF
        && b[3] == 0x9B
        && b[4..12].iter().all(|&x| x == 0)
    {
        return is_private_v4(Ipv4Addr::new(b[12], b[13], b[14], b[15]));
    }
    if b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8 {
        return true;
    }
    b[0] == 0xFF
        || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)
        || (b[0] & 0xFE) == 0xFC
        || b.iter().all(|&x| x == 0)
}

pub fn host_is_loopback_name(host: &str) -> bool {
    host.eq_ignore_ascii_case("localhost") || host == "127.0.0.1" || host == "::1"
}

/// Registration check including DNS. Unresolvable hostnames are accepted
/// (dispatcher re-resolves). Error strings match C# `OutboundUrl`.
pub fn validate_outbound_url(raw: &str, allow_loopback: bool) -> Result<String, &'static str> {
    let raw = raw.trim();
    if raw.is_empty() {
        return Err("url is required");
    }
    let Ok(uri) = reqwest::Url::parse(raw) else {
        return Err("url is required");
    };
    if uri.scheme() != "http" && uri.scheme() != "https" {
        return Err("url must be http or https");
    }
    let Some(host) = uri.host_str() else {
        return Err("url is required");
    };
    let parsed_ip = parse_host_ip(host);
    if let Some(ip) = parsed_ip {
        if is_disallowed(ip, allow_loopback) {
            return Err("url is not allowed");
        }
        return Ok(uri.to_string());
    }
    if host_is_loopback_name(host) {
        if allow_loopback {
            return Ok(uri.to_string());
        }
        return Err("url is not allowed");
    }
    if let Ok(addrs) = (host, 0u16).to_socket_addrs() {
        for sa in addrs {
            if is_disallowed(sa.ip(), allow_loopback) {
                return Err("url is not allowed");
            }
        }
    }
    Ok(uri.to_string())
}

fn parse_host_ip(host: &str) -> Option<IpAddr> {
    host.parse().ok().or_else(|| {
        host.trim_start_matches('[')
            .trim_end_matches(']')
            .parse()
            .ok()
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::net::IpAddr;

    fn v4(s: &str) -> IpAddr {
        s.parse().unwrap()
    }
    fn v6(s: &str) -> IpAddr {
        s.parse().unwrap()
    }

    #[test]
    fn rfc1918_and_link_local_disallowed() {
        assert!(is_private_or_loopback(v4("10.0.0.1")));
        assert!(is_private_or_loopback(v4("169.254.169.254")));
        assert!(is_private_or_loopback(v4("192.168.1.1")));
        assert!(is_private_or_loopback(v4("172.16.0.1")));
        assert!(is_disallowed(v4("10.0.0.1"), false));
    }

    #[test]
    fn loopback_allowed_only_in_testing() {
        assert!(is_private_or_loopback(v4("127.0.0.1")));
        assert!(!is_disallowed(v4("127.0.0.1"), true));
        assert!(is_disallowed(v4("127.0.0.1"), false));
    }

    #[test]
    fn ipv6_ula_doc_nat64() {
        assert!(is_private_or_loopback(v6("fc00::1")));
        assert!(is_private_or_loopback(v6("2001:db8::1")));
        assert!(is_private_or_loopback(v6("64:ff9b::10.0.0.1")));
        assert!(is_private_or_loopback(v6("::ffff:10.0.0.1")));
    }

    #[test]
    fn public_v4_allowed() {
        assert!(!is_private_or_loopback(v4("8.8.8.8")));
        assert!(!is_disallowed(v4("8.8.8.8"), false));
    }

    #[test]
    fn validate_loopback_and_metadata() {
        assert!(validate_outbound_url("http://127.0.0.1:9/x", true).is_ok());
        assert_eq!(
            validate_outbound_url("http://127.0.0.1/hook", false).unwrap_err(),
            "url is not allowed"
        );
        assert_eq!(
            validate_outbound_url("http://169.254.169.254/", false).unwrap_err(),
            "url is not allowed"
        );
        assert_eq!(
            validate_outbound_url("http://[::1]/hook", false).unwrap_err(),
            "url is not allowed"
        );
        assert!(validate_outbound_url("https://app.example/hook", false).is_ok());
        assert_eq!(
            validate_outbound_url("", true).unwrap_err(),
            "url is required"
        );
        assert_eq!(
            validate_outbound_url("ftp://app.example/hook", false).unwrap_err(),
            "url must be http or https"
        );
    }
}
