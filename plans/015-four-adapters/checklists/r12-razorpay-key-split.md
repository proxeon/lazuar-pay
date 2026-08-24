# R12 — Split key_id:key_secret for Basic auth

**Track:** Razorpay · **Depends:** R11  
**Analysis:** Hub `GetClient` `apiKey.Split(':')`  
**IDs:** —  
**Goal:** Payment Links HTTP Basic is `key_id` user, `key_secret` password.

---

## R12.1

- [x] Unprotect ciphertext, split on first `:`
- [x] Missing secret part → 503 incomplete rail (do not call Razorpay)
- [x] `Authorization: Basic base64(key_id + ":" + key_secret)`

## R12.2 Exit

- [x] Helper unit test
