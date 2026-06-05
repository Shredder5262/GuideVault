# OPDS

Guidevault includes OPDS support for compatible reading clients.

OPDS can be enabled or disabled from the server settings area.

The default local base URL is:

```text
http://localhost:5478
```

## Keys

Guidevault can generate OPDS access keys. Use a generated key in compatible clients when authentication is required.

## Docker note

For OPDS access from another device, use the host or server address that the device can reach.

Example:

```text
http://192.168.1.50:5478
```

Do not use `localhost` from a phone or tablet unless Guidevault is running on that same device.
