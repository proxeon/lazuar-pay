# W1-LP-135 — done

VitePress `reference/events.md` is the **v1** catalog: envelope, shipped types only, not-in-v1 table (`payment.refunded`, `invoice.submitted`, `invoice.cancelled`, `subscription.updated`). Developers `/webhooks` banners to VitePress and no longer lists submitted/cancelled as live. Cross-links from index, webhooks guide, api-versioning, how-to-maintain.

## Tests run

- `pnpm --filter lazuar-docs build` — **ok**
- Grep: shipped types on events.md; forbidden types only in the not-in-v1 table

Not committed. Not pushed.

Tracker `LP-135` **P → Y**.
