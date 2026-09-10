# Gateway `--file` shapes

Redacted TypeSpec `PutGateway` bodies for `lazuar-pay gateway put --file`.
**Never commit live `sk_` / PEM / `whsec_`.** Copy, fill, `chmod 600`.

CLI refuses group/world-readable files (mode `0644`).

```sh
chmod 600 stripe.json
lazuar-pay gateway put --file ./stripe.json
```
