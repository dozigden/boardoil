#!/bin/sh
set -eu

. /etc/os-release
case "$ID:$VERSION_ID" in
  debian:12)
    # Debian DLA-4845-1: https://lists.debian.org/debian-lts-announce/2026/10/msg00039.html
    minimum_version='20250419~deb12u2'
    ;;
  ubuntu:24.04)
    # Ubuntu USN-8944-1: https://ubuntu.com/security/notices/USN-8944-1
    minimum_version='20260816ubuntu0.24.04.1'
    ;;
  *)
    echo "No reviewed CA trust-store minimum for $ID $VERSION_ID." >&2
    exit 1
    ;;
esac

apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends ca-certificates
rm -rf /var/lib/apt/lists/*

installed_version="$(dpkg-query --show --showformat='${Version}' ca-certificates)"
if ! dpkg --compare-versions "$installed_version" ge "$minimum_version"; then
  echo "CA bundle $installed_version predates the eMudhra fix ($minimum_version)." >&2
  exit 1
fi

# These roots must not remain in the generated OpenSSL trust directory.
for certificate in emSign_ECC_Root_CA_-_G3 AAA_Certificate_Services; do
  if [ -e "/etc/ssl/certs/$certificate.pem" ]; then
    echo "Distrusted root remains trusted: $certificate" >&2
    exit 1
  fi
done

echo "Verified CA bundle $installed_version for $ID $VERSION_ID."
