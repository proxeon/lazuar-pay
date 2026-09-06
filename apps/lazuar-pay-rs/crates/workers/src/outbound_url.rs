//! Port of `OutboundUrl.cs` private-range checks (issue 017 / 028 P1-15).

use std::net::{IpAddr, Ipv4Addr, Ipv6Addr};

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
}
