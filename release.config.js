const path = require('path');

module.exports = {
  branches: [
    "main"
  ],
  plugins: [
    [
      '@semantic-release/commit-analyzer',
      {
        preset: 'conventionalcommits',
        releaseRules: [
          {breaking: true, release: 'major'},
          {type: 'docs', scope:'README', release: 'patch'},
          {type: 'perf', release: 'patch'},
          {type: 'fix', release: 'patch'},
          {type: 'deps', release: 'patch'},
          {type: "chore", release: false },
          {type: 'feat', release: 'minor'},
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
          }
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
        "verifyReleaseCmd": "echo '##vso[task.setvariable variable=PackageVersion]${nextRelease.version}'",
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