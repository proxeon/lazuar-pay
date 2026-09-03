//! Port of `SolanaBase58.cs` — Bitcoin-alphabet base58 decode/encode.

const ALPHABET: &[u8; 58] = b"123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

pub fn decode(input: &str) -> Option<Vec<u8>> {
    if input.is_empty() {
        return None;
    }
    // Little-endian accumulator; reversed at the end (canonical b58 decode).
    let mut output: Vec<u8> = Vec::with_capacity(input.len());
    for ch in input.bytes() {
        let value = ALPHABET.iter().position(|a| *a == ch)? as u32;
        let mut carry = value;
        for byte in output.iter_mut() {
            carry += (*byte as u32) * 58;
            *byte = (carry & 0xff) as u8;
            carry >>= 8;
        }
        while carry > 0 {
            output.push((carry & 0xff) as u8);
            carry >>= 8;
        }
    }
    // Leading '1's are leading zero bytes.
    for ch in input.bytes() {
        if ch == b'1' {
            output.push(0);
        } else {
            break;
        }
    }
    output.reverse();
    Some(output)
}

pub fn encode(input: &[u8]) -> String {
    let mut digits: Vec<u8> = Vec::with_capacity(input.len() * 138 / 100 + 1);
    for byte in input {
        let mut carry = *byte as u32;
        for digit in digits.iter_mut() {
            carry += (*digit as u32) << 8;
            *digit = (carry % 58) as u8;
            carry /= 58;
        }
        while carry > 0 {
            digits.push((carry % 58) as u8);
            carry /= 58;
        }
    }
    let mut out = String::new();
    for byte in input {
        if *byte == 0 {
            out.push('1');
        } else {
            break;
        }
    }
    for digit in digits.iter().rev() {
        out.push(ALPHABET[*digit as usize] as char);
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips_known_values() {
        let bytes = b"hello world";
        assert_eq!(encode(bytes), "StV1DL6CwTryKyV");
        assert_eq!(decode("StV1DL6CwTryKyV").as_deref(), Some(bytes.as_slice()));
    }

    #[test]
    fn decode_rejects_ambiguous_characters() {
        assert!(decode("0OIl").is_none());
    }

    #[test]
    fn leading_ones_preserve_zero_bytes() {
        let decoded = decode("112233").expect("valid");
        assert_eq!(decoded[..2], [0, 0]);
    }
}
