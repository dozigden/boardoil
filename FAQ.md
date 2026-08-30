# BoardOil FAQ

For initial setup, see [Getting Started](GETTING_STARTED.md). For HTTPS, backups, and other operational details, see [Advanced Installation](ADVANCED_INSTALLATION.md).

## Slicks

### Why don't slicks span columns in Safari?

Safari does not like how slicks are rendered, so it has a degraded experience that does not include column spanning, sorry I've tried. Talk to Apple.

If you're curious, add one of these to the board URL to force a renderer:

- `?gooRenderer=full`
- `?gooRenderer=lite`

## Installation and data

### How do I move a Board to a new installation of BoardOil?

On the old installation, open **Board Configuration → Details** and select **Export**.

On the new installation, open **Boards → Create board → Import package**, select the exported ZIP, then select **Import board**.
