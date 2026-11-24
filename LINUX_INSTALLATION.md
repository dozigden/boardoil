# Install BoardOil on Linux with APT

The `.deb` targets Debian-based systems with systemd on `amd64` and `arm64`. APT must be able to resolve `adduser` and `aspnetcore-runtime-10.0`. [Debian runtime setup](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian) · [Ubuntu runtime setup](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install)

## Install

Download the `.deb` for your architecture and its `.sha256` into an empty directory. The first packages are currently in a draft release. [BoardOil releases](https://github.com/dozigden/boardoil/releases)

```sh
sha256sum --check --strict *.deb.sha256 && sudo apt install ./*.deb
```

The package enables and starts `boardoil.service` as the `boardoil` user.

## HTTPS and first login

Proxy HTTPS to `127.0.0.1:5000` on the same host. Preserve the host, forward `X-Forwarded-Proto: https`, and enable WebSockets. For Caddy, use this site block.

```caddyfile
boards.example.org {
    reverse_proxy 127.0.0.1:5000
}
```

Restrict access until you complete **Create Initial Admin** at the HTTPS address.

## Service and data

| Path | Purpose |
| --- | --- |
| `/etc/boardoil/boardoil.env` | Service configuration. Restart `boardoil` after editing. |
| `/var/lib/boardoil/images/` | Uploaded images, including user profile pictures |
| `/var/lib/boardoil/attachments/` | Private card attachments, including images attached to cards |
| `/var/lib/boardoil/boardoil.db` | SQLite database |
| `/var/lib/boardoil/boardoil-auth-signing-key` | Generated authentication signing key |
| `/usr/lib/boardoil/` | Packaged application files |

Check service status with `systemctl status boardoil` and logs with `journalctl -u boardoil`.

## Upgrades and recovery

Before upgrading, stop the service and back up `/etc/boardoil/` and `/var/lib/boardoil/` together, including any custom storage paths. Protect the backup and retain the matching `.deb` for rollback.

Verify and install the new package with the install command above. Keep local changes to `boardoil.env` if prompted. BoardOil applies migrations on startup. Open an existing board and attachment to check the upgrade.

**Rollback after a migration.** Stop the service and run `sudo apt remove boardoil`. Move the current configuration and data directories aside, restore the pre-upgrade backup with ownership and permissions intact, then install the matching older package. This returns boards and uploads to the time of the backup.

**Removal.** `sudo apt remove boardoil` keeps the configuration and data for reinstallation.
