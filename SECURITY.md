# Security Policy

## Supported Versions

| Version | Supported |
| ------- | --------- |
| 1.x.x   | Yes       |
| < 1.0   | No        |

Only the latest major version receives security updates. Users on older versions should upgrade to receive fixes.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them privately using one of the following methods:

1. **GitHub Private Vulnerability Reporting** (preferred): Use the [Security Advisories](https://github.com/eriklieben/ErikLieben.FA.ES/security/advisories/new) feature to report privately.

2. **Email**: Contact the maintainer directly (see GitHub profile for contact information).

### What to Include

When reporting a vulnerability, please include:

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

### What to Expect

- **Acknowledgment**: Within 7 days
- **Initial assessment**: Within 14 days
- **Resolution timeline**: Depends on severity and complexity

| Severity | Target Resolution |
|----------|-------------------|
| Critical | 7 days            |
| High     | 30 days           |
| Medium   | 90 days           |
| Low      | Best effort       |

### Disclosure Policy

- Please allow reasonable time to address the vulnerability before public disclosure
- Coordinated disclosure is appreciated
- Credit will be given to reporters (unless anonymity is requested)

## Security Best Practices for Users

When using this library:

- Keep dependencies up to date
- Use the latest supported version
- Review the [changelog](docs/CHANGELOG.md) for security-related updates
- Enable GitHub Dependabot/Renovate for automated updates

## Security Measures in This Project

This project implements several security measures:

- **Dependency scanning**: Snyk and Renovate for vulnerability detection
- **Static analysis**: SonarCloud for code quality and security issues
- **OpenSSF Scorecard**: Continuous security posture assessment
- **Build attestations**: SLSA provenance for published packages
- **SBOM**: Software Bill of Materials included with releases
