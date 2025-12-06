const path = require('node:path');

module.exports = {
  branches: [
    "main",
    {
      name: "vnext",
      prerelease: "preview",
      channel: "preview"
    }
  ],
  // Override version for vnext branch to target 2.0.0 previews
  tagFormat: "${version}",
  plugins: [
    [
      '@semantic-release/commit-analyzer',
      {
        preset: 'conventionalcommits',
        releaseRules: [
          // Only these types trigger releases
          {breaking: true, release: 'major'},
          {type: 'feat', release: 'minor'},
          {type: 'fix', release: 'patch'},
          // Manual release trigger - use when you want to release accumulated changes
          {type: 'release', release: 'patch'},
          // Block all other types (including deps from Renovate)
          {type: 'deps', release: false},
          {type: 'chore', release: false},
          {type: 'docs', release: false},
          {type: 'perf', release: false},
          {type: 'style', release: false},
          {type: 'test', release: false},
          {type: 'refactor', release: false},
          {type: 'ci', release: false},
          {type: 'build', release: false}
        ],
        parserOpts: {
          noteKeywords: [
            'BREAKING CHANGE',
            'BREAKING CHANGES'
          ]
        }
      }
    ],
    ['@semantic-release/release-notes-generator', {
      preset: 'conventionalcommits',
      presetConfig: {
        types: [
          {
            type: 'docs',
            section: '📚 Documentation',
            hidden: false
          },
          {
            type: 'fix',
            section: '🐛 Bug fixes',
            hidden: false
          },
          {
            type: 'feat',
            section: '✨ New features',
            hidden: false
          },
          {
            type: 'perf',
            section: '⚡ Performance improvement',
            hidden: false
          },
          {
            type: 'style',
            section: '💄 Code style adjustments',
            hidden: false
          },
          {
            type: 'test',
            section: '🧪 (Unit)test cases adjusted',
            hidden: false
          },
          {
            type: 'refactor',
            section: '♻️ Refactor',
            hidden: false
          },
          {
            type: 'chore',
            scope: 'deps',
            section: '⬆️ Dependency updates',
            hidden: false
          },
          {
            type: 'chore',
            scope: 'test-deps',
            section: '🧪 Test dependency updates',
            hidden: true  // Hide test dependencies from release notes
          },
          {
            type: 'release',
            section: '🚀 Release',
            hidden: false
          },
        ]
      },
      parserOpts: {
        noteKeywords: [
          'BREAKING CHANGE',
          'BREAKING CHANGES'
        ]
      }
    }],
    [
      '@semantic-release/changelog',
      {
        changelogFile: 'docs/CHANGELOG.md'
      }
    ],
    [
      "@semantic-release/exec",
      {
        "prepareCmd": "pwsh -File ./build-packages.ps1 -PackageVersion ${nextRelease.version}"
      }
    ],
    [
      '@semantic-release/npm', {
        npmPublish: false
      }
    ],
    [
      '@semantic-release/git',
      {
        assets: ['docs/CHANGELOG.md', 'package.json'],
        message: 'chore(release): ${nextRelease.version} [skip ci]\n\n${nextRelease.notes}'
      }
    ],
    [
      '@semantic-release/github',
      {
        assets: [
          'docs/CHANGELOG.md',
          // Include all .nupkg and .snupkg files from release-artifacts directory
          'release-artifacts/*.nupkg',
          'release-artifacts/*.snupkg'
        ]
      }
    ]
  ]
}
