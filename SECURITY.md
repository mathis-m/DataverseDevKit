# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Dataverse DevKit, please report it by emailing the maintainers. **Do not open a public issue.**

Please include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Any suggested fixes

We will acknowledge your email within 48 hours and provide a more detailed response within 7 days.

## Security Best Practices

Dataverse DevKit implements several security measures:
- Process isolation for plugins
- Sandboxed permissions via capability manifests
- Secure credential storage using OS keychains
- No external network ports (IPC via named pipes/UDS)
- Content Security Policy for WebView

For more details, see [docs/security-and-permissions.md](docs/security-and-permissions.md).
