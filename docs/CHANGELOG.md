## [2.0.0-preview.8](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.7...2.0.0-preview.8) (2026-01-07)

### ✨ New features

* **table-storage:** add conditional payload chunking for large events ([474db9d](https://github.com/eriklieben/ErikLieben.FA.ES/commit/474db9d26651205b4b17b4e8447abab6f39d1791))

## [2.0.0-preview.7](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.6...2.0.0-preview.7) (2026-01-07)

### 🐛 Bug fixes

* **live-migration:** reload document before close to prevent hash conflict ([37995c8](https://github.com/eriklieben/ErikLieben.FA.ES/commit/37995c8cdd4e22a93c4c6f33955f07165b0fc00b))
* **tests:** resolve CosmosDB integration test failures in CI ([bbff8fb](https://github.com/eriklieben/ErikLieben.FA.ES/commit/bbff8fb7954ac0a0524c7bc52e1f7277bf1aeda7))
* **tests:** resolve serializer stream disposal and test parallelism ([246e942](https://github.com/eriklieben/ErikLieben.FA.ES/commit/246e9423bfc2bcafe853c57d30eebc623c285f5f))

### ✨ New features

* add TaskFlow demo application and comprehensive documentation ([abb3c7c](https://github.com/eriklieben/ErikLieben.FA.ES/commit/abb3c7cc9f86f25d3cf39e38a6698f1e838d2f94))
* production readiness features for v2.0.0 ([0209f91](https://github.com/eriklieben/ErikLieben.FA.ES/commit/0209f91aacd0f686554f10e76c4c474042e9e970))
* **scale:** add streaming reads, closed stream cache, and AOT serialization ([798ddb8](https://github.com/eriklieben/ErikLieben.FA.ES/commit/798ddb8405d9d3ea29b278d7bc657699cc6312cd))

## [2.0.0-preview.6](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.5...2.0.0-preview.6) (2026-01-05)

### 🐛 Bug fixes

* **live-migration:** prevent infinite loop when source stream already closed ([3520562](https://github.com/eriklieben/ErikLieben.FA.ES/commit/352056218cd615f221d9fae742df0ee983dfe052))

## [2.0.0-preview.5](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.4...2.0.0-preview.5) (2026-01-05)

### ✨ New features

* implement migration dry-run and backup execution ([804272b](https://github.com/eriklieben/ErikLieben.FA.ES/commit/804272bb055e4c870c4fdf9dba1abfe254fc23c7))
* implement migration verification, rollback, and book closing ([4ebba9e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/4ebba9e932af56be64ea3763831c6ddfa9f83447))
* implement stream tag support ([50badb8](https://github.com/eriklieben/ErikLieben.FA.ES/commit/50badb81954da583e525c7dffabffe63d023a016))
* **live-migration:** add per-event callbacks for migration progress ([f7dd08f](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f7dd08fd1de712ca0b66f2e037bd7d930e3f01d1))

## [2.0.0-preview.4](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.3...2.0.0-preview.4) (2026-01-05)

### 🐛 Bug fixes

* prevent duplicate generic parameters in projection and aggregate code generation ([fa35522](https://github.com/eriklieben/ErikLieben.FA.ES/commit/fa35522befb9552ec6cd9c71e229d88a9c207432))

## [2.0.0-preview.3](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.2...2.0.0-preview.3) (2026-01-05)

### 🐛 Bug fixes

* prevent duplicate generic parameters in JsonSerializable attributes ([468e131](https://github.com/eriklieben/ErikLieben.FA.ES/commit/468e1316b1bfda93f9d9d3666535959ba24ef52d))

## [2.0.0-preview.2](https://github.com/eriklieben/ErikLieben.FA.ES/compare/2.0.0-preview.1...2.0.0-preview.2) (2025-12-30)

### 📚 Documentation

* add markdown documentation ([046d650](https://github.com/eriklieben/ErikLieben.FA.ES/commit/046d650e2c7a9a2412c1fcae08bd66c9089c2b33))

### 🐛 Bug fixes

* don't generate type names that are already generic types ([8d542cd](https://github.com/eriklieben/ErikLieben.FA.ES/commit/8d542cdaea3d6e538d461dee6a0064d7408619d1))

### ✨ New features

* add RemoveAsync/RemoveTagAsync for document tag removal ([70dfab2](https://github.com/eriklieben/ErikLieben.FA.ES/commit/70dfab2cfa39abdc2a262a14a7b83b53166724df))
* include live migrations and extend test cases ([040264e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/040264e5de915679aa0418138c9521315d0d0c79))

### 💄 Code style adjustments

* resolve sonarcloud issues ([5000d5c](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5000d5c059bbe93f1e4d12107af858947eab3e45))
* resolve sonarcloud issues ([b843f78](https://github.com/eriklieben/ErikLieben.FA.ES/commit/b843f78c98ced401bbae307a15559230df39bf3a))
* resolve sonarcloud issues ([43a4fe3](https://github.com/eriklieben/ErikLieben.FA.ES/commit/43a4fe313e941e4065b0e2b03b55dd3de10bc4da))
* resolve sonarcloud issues ([4363332](https://github.com/eriklieben/ErikLieben.FA.ES/commit/4363332ef9ce7f9ccb7329e516dfb2942b7f6a4a))
* resolve sonarcloud issues ([e32cf7a](https://github.com/eriklieben/ErikLieben.FA.ES/commit/e32cf7a06385a6b4d592eb030e7b7a68714d6278))
* resolve sonarcloud issues ([8fde5a9](https://github.com/eriklieben/ErikLieben.FA.ES/commit/8fde5a98034bdbf6bb1c1f9faddf919414d3273d))
* resolve sonarcloud issues ([0bab9bf](https://github.com/eriklieben/ErikLieben.FA.ES/commit/0bab9bf51109b9ebc80c7efed161e2a58b6187f5))
* resolve sonarcloud issues ([5bb1d98](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5bb1d985a5ffc755b33ed5295ce682483355acbb))
* resolve sonarcloud issues ([9a7d1ea](https://github.com/eriklieben/ErikLieben.FA.ES/commit/9a7d1ea4cffa6c972448b97c4a87ec10004b3337))
* resolve sonarcloud issues ([5722369](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5722369bab23507036df8fb5c0b3362b0615b11e))

### 🧪 (Unit)test cases adjusted

* add RemoveAsync test cases for all tag store implementations ([e0b9517](https://github.com/eriklieben/ErikLieben.FA.ES/commit/e0b9517ef07086f206bddeb872a2635f79af0668))
* extend test coverage for CosmosDb and EventStreamManagement ([6712325](https://github.com/eriklieben/ErikLieben.FA.ES/commit/67123253b6d80fe038971a281c89c51ee54dd865))
* extend tests cases ([2d0b21e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/2d0b21e568291a44b46be9be06fb5c1590219eee))
* fix test cases for new ILogger generator ([a8c93f4](https://github.com/eriklieben/ErikLieben.FA.ES/commit/a8c93f4df5ee8984d9d17c3a78cadd9e8654b12d))
* fix test cases for new ILogger generator ([24e0253](https://github.com/eriklieben/ErikLieben.FA.ES/commit/24e0253aa65fdec9319832915e891e3f2d386f50))
* improve test coverage ([a85669d](https://github.com/eriklieben/ErikLieben.FA.ES/commit/a85669df560b606d6e6753ee03e4d399c5983be9))
* improve tests and setup usage of coverage.runsettings ([f39d2ef](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f39d2ef904faae07e9bee375be5025094fce0bb1))
* make the tests work on linux env as well ([8d91842](https://github.com/eriklieben/ErikLieben.FA.ES/commit/8d918429fd729f7eb65d4dbb2922a3b7533ff7f6))
* resolve test failures ([9f00af8](https://github.com/eriklieben/ErikLieben.FA.ES/commit/9f00af8d29e2dc39354548f1672b3c9a734b3104))
* update test coverage ([f298f61](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f298f61712936af0848e08fe85070bb8deefbd0d))

## [2.0.0-preview.1](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v2.0.0-preview.0...2.0.0-preview.1) (2025-12-07)

### 🐛 Bug fixes

* publish nuget package as preview to prod nuget.org ([7b57e12](https://github.com/eriklieben/ErikLieben.FA.ES/commit/7b57e129142cdc1ecefff58ec21b129e32a23bbd))

## [1.4.0-preview.1](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.6...1.4.0-preview.1) (2025-12-07)

### ✨ New features

* combined changes for v2 ([159acf3](https://github.com/eriklieben/ErikLieben.FA.ES/commit/159acf3de8acc75c92587e17aca3f671cd082e83))

## [1.3.7](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.6...1.3.7) (2026-02-13)

### 🐛 Bug fixes

* emit valid C# for projections with empty namespaces and extra constructor parameters ([#61](https://github.com/eriklieben/ErikLieben.FA.ES/issues/61)) ([0381530](https://github.com/eriklieben/ErikLieben.FA.ES/commit/03815306077057c0eabb7ba33dd013c3245ec0cc))

### ⬆️ Dependency updates

* **deps:** Update dependency Azure.Identity to 1.17.1 ([#53](https://github.com/eriklieben/ErikLieben.FA.ES/issues/53)) ([f529cc2](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f529cc25ebad095d76397b8f8092597188a9b20b))
* **deps:** Update dependency dotnet-sdk to 9.0.308 ([#47](https://github.com/eriklieben/ErikLieben.FA.ES/issues/47)) ([5396e25](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5396e25db9527a1eb3d823126208dfd082b934d7))
* **deps:** Update dependency Spectre.Console to 0.54.0 ([#52](https://github.com/eriklieben/ErikLieben.FA.ES/issues/52)) ([c87055d](https://github.com/eriklieben/ErikLieben.FA.ES/commit/c87055d3069151bfbdc7d847375bebd57159e699))
* **deps:** Update dependency Spectre.Console.Testing to 0.54.0 ([#51](https://github.com/eriklieben/ErikLieben.FA.ES/issues/51)) ([3de4d3b](https://github.com/eriklieben/ErikLieben.FA.ES/commit/3de4d3b857b320ef2abb311ba92eee8439cfd457))
* **deps:** Update dependency System.Formats.Asn1 to 9.0.11 ([#48](https://github.com/eriklieben/ErikLieben.FA.ES/issues/48)) ([49c4ae4](https://github.com/eriklieben/ErikLieben.FA.ES/commit/49c4ae469746409b08d54c91925ddd645787353d))

## [1.3.6](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.5...v1.3.6) (2025-11-12)

### 🐛 Bug fixes

* disable source gen attribute on interfaces ([cf5415a](https://github.com/eriklieben/ErikLieben.FA.ES/commit/cf5415a32eaf86a2c1665d1972a9b816077146d9))

## [1.3.5](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.4...v1.3.5) (2025-11-12)

### 🐛 Bug fixes

* exclude generated code from code coverage and make sure identifier of interface is added to usings ([#45](https://github.com/eriklieben/ErikLieben.FA.ES/issues/45)) ([fc40744](https://github.com/eriklieben/ErikLieben.FA.ES/commit/fc40744af382b18f93c13df21b5525f96247389a))

## [1.3.4](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.3...v1.3.4) (2025-11-12)

### 🐛 Bug fixes

* previous correction caused issues with generic types, this fixes… ([#44](https://github.com/eriklieben/ErikLieben.FA.ES/issues/44)) ([311ddd0](https://github.com/eriklieben/ErikLieben.FA.ES/commit/311ddd039b99d90c07bc739ff69420939d103a49))

## [1.3.3](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.2...v1.3.3) (2025-11-12)

### 🐛 Bug fixes

* correctly generate code with adjusted Store, don't add escape ch… ([#43](https://github.com/eriklieben/ErikLieben.FA.ES/issues/43)) ([0ce8c72](https://github.com/eriklieben/ErikLieben.FA.ES/commit/0ce8c72da9257a86204c0b50797be753de1600ba))

## [1.3.2](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.1...v1.3.2) (2025-11-12)

### 🐛 Bug fixes

* correctly generate code with adjusted Store, don't add escape ch… ([#42](https://github.com/eriklieben/ErikLieben.FA.ES/issues/42)) ([1c82caa](https://github.com/eriklieben/ErikLieben.FA.ES/commit/1c82caaed6fa20909291f0cbd59f9def2c3d1e8e))

### ⬆️ Dependency updates

* **deps:** Update dependency semantic-release to 25.0.2 ([#41](https://github.com/eriklieben/ErikLieben.FA.ES/issues/41)) ([d49fe21](https://github.com/eriklieben/ErikLieben.FA.ES/commit/d49fe2142d23edf92e665615ae0e3eac0238f915))

## [1.3.1](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.3.0...v1.3.1) (2025-11-03)

### 🐛 Bug fixes

* correct path in linux for .elfa, add [#nullable](https://github.com/eriklieben/ErikLieben.FA.ES/issues/nullable), accept objectnam… ([#40](https://github.com/eriklieben/ErikLieben.FA.ES/issues/40)) ([3a23d94](https://github.com/eriklieben/ErikLieben.FA.ES/commit/3a23d948752bf73bff0b218f55fdf1fdbe2cc74f))

### ⬆️ Dependency updates

* **deps:** Update dependency node to v24 ([#39](https://github.com/eriklieben/ErikLieben.FA.ES/issues/39)) ([2dd19c5](https://github.com/eriklieben/ErikLieben.FA.ES/commit/2dd19c5b985b6f81f32055d2961ca97fbeeef462))
* **deps:** Update dependency Pastel to 7.0.1 ([#38](https://github.com/eriklieben/ErikLieben.FA.ES/issues/38)) ([5d005cb](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5d005cbc6cace2fdf1975b0a4fc4f3c021831b43))

## [1.3.0](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.2.0...v1.3.0) (2025-10-28)

### ✨ New features

* allow EventStreamBlobSettings on top of Aggregate ([#37](https://github.com/eriklieben/ErikLieben.FA.ES/issues/37)) ([2d3ed5c](https://github.com/eriklieben/ErikLieben.FA.ES/commit/2d3ed5c5b62b012a95d59c9bdcab14bd766b0fdc))

## [1.2.0](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.1.2...v1.2.0) (2025-10-27)

### 📚 Documentation

* add xml documentation on public surface ([#32](https://github.com/eriklieben/ErikLieben.FA.ES/issues/32)) ([1c76aad](https://github.com/eriklieben/ErikLieben.FA.ES/commit/1c76aadf1df845db606a6bfada14aeb122fb36d4))

### ✨ New features

* GetFirstByDocumentTag(string tag) should return Task<T?> not Ta… ([#36](https://github.com/eriklieben/ErikLieben.FA.ES/issues/36)) ([09b5be0](https://github.com/eriklieben/ErikLieben.FA.ES/commit/09b5be0597f1a0c328eb35922f6066e4340dd020))

### 💄 Code style adjustments

* reduce cyclomatic complexity ([#33](https://github.com/eriklieben/ErikLieben.FA.ES/issues/33)) ([01ba5ac](https://github.com/eriklieben/ErikLieben.FA.ES/commit/01ba5ace99ac5cf4031da695a480abd981f4d073))
* resolve Sonarcloud issues ([#34](https://github.com/eriklieben/ErikLieben.FA.ES/issues/34)) ([823c222](https://github.com/eriklieben/ErikLieben.FA.ES/commit/823c222b8a07ace72e15d660c30ebce05809884f))
* resolve sonarcloud issues ([#35](https://github.com/eriklieben/ErikLieben.FA.ES/issues/35)) ([2e60699](https://github.com/eriklieben/ErikLieben.FA.ES/commit/2e60699e6492709293d75cdfc8aeec9d4105f635))
* resolve Sonarcloud issues and small bump of dependencies ([#31](https://github.com/eriklieben/ErikLieben.FA.ES/issues/31)) ([77c4dff](https://github.com/eriklieben/ErikLieben.FA.ES/commit/77c4dff2b50e51104f2842ffca0a05c7b4f9e17c))

### ⬆️ Dependency updates

* **deps:** Update dependency semantic-release to v25 ([#29](https://github.com/eriklieben/ErikLieben.FA.ES/issues/29)) ([7c9e094](https://github.com/eriklieben/ErikLieben.FA.ES/commit/7c9e094dae3746e8aa117f506726b7138c7f786e))

## [1.1.2](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.1.1...v1.1.2) (2025-10-21)

### 🐛 Bug fixes

* reference correct project from azfunc worker extentions ([#30](https://github.com/eriklieben/ErikLieben.FA.ES/issues/30)) ([2c29199](https://github.com/eriklieben/ErikLieben.FA.ES/commit/2c29199b06281aa52dd8139c7e96517fee4b2d51))

### ⬆️ Dependency updates

* **deps:** Update dependency Azure.Identity to 1.17.0 ([#27](https://github.com/eriklieben/ErikLieben.FA.ES/issues/27)) ([d8b4c0d](https://github.com/eriklieben/ErikLieben.FA.ES/commit/d8b4c0d744d761c91fa36b3a4ae5e92715e767e6))
* **deps:** Update dependency Microsoft.Bcl.AsyncInterfaces to 9.0.10 ([#24](https://github.com/eriklieben/ErikLieben.FA.ES/issues/24)) ([a239a0e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/a239a0e67b429f71f04d33b854c48ed4b3827a3f))
* **deps:** Update dependency Microsoft.Extensions.DependencyInjection to 9.0.10 ([#21](https://github.com/eriklieben/ErikLieben.FA.ES/issues/21)) ([fc18cce](https://github.com/eriklieben/ErikLieben.FA.ES/commit/fc18ccefba64f92729d24a10fbe0f1dd3ee25043))
* **deps:** Update dependency Microsoft.NET.Test.Sdk to v18 ([#23](https://github.com/eriklieben/ErikLieben.FA.ES/issues/23)) ([f38881d](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f38881d0a544a87edc5cb5739b5f7a1772517d0e))
* **deps:** Update dependency semantic-release to 24.2.9 ([#25](https://github.com/eriklieben/ErikLieben.FA.ES/issues/25)) ([a648e15](https://github.com/eriklieben/ErikLieben.FA.ES/commit/a648e15aff45e7a5d5d302ae78ad458e1fd204bb))
* **deps:** Update dependency Spectre.Console.Testing to 0.52.0 ([#22](https://github.com/eriklieben/ErikLieben.FA.ES/issues/22)) ([82c86bc](https://github.com/eriklieben/ErikLieben.FA.ES/commit/82c86bcf20a926cd0bb5d003eeb9ca38ed64b63a))
* **deps:** Update dependency System.Formats.Asn1 to 9.0.10 ([#26](https://github.com/eriklieben/ErikLieben.FA.ES/issues/26)) ([1c2c103](https://github.com/eriklieben/ErikLieben.FA.ES/commit/1c2c10314f59823654d477178a792301b8d99035))

## [1.1.1](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.1.0...v1.1.1) (2025-10-19)

### 🐛 Bug fixes

* security upgrade Microsoft.Build.Tasks.Core from 17.14.8 to 17.14.28 ([#20](https://github.com/eriklieben/ErikLieben.FA.ES/issues/20)) ([a587dbb](https://github.com/eriklieben/ErikLieben.FA.ES/commit/a587dbbbc829dedb5949ee7e57b8f3d028f83ce4))

### 💄 Code style adjustments

* resolve sonar cloud warnings ([#19](https://github.com/eriklieben/ErikLieben.FA.ES/issues/19)) ([8d3d02e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/8d3d02e291ef593b48cf3f6c2486f00300a2f2bd))
* resolve sonarcloud issues and add xml documentation ([#18](https://github.com/eriklieben/ErikLieben.FA.ES/issues/18)) ([5401b7f](https://github.com/eriklieben/ErikLieben.FA.ES/commit/5401b7f9d7b61d718f84424754604c9482125b4d))

## [1.1.0](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.0.1...v1.1.0) (2025-09-28)

### ✨ New features

* remove generic exceptions and add exception documentation ([#17](https://github.com/eriklieben/ErikLieben.FA.ES/issues/17)) ([f1ee465](https://github.com/eriklieben/ErikLieben.FA.ES/commit/f1ee465c92ba7da78bb1d0e69f6c6b8df8d7b0ce))

### ⬆️ Dependency updates

* **deps:** Update dependency @semantic-release/release-notes-generator to 14.1.0 ([#7](https://github.com/eriklieben/ErikLieben.FA.ES/issues/7)) ([4791028](https://github.com/eriklieben/ErikLieben.FA.ES/commit/47910287e155e479475825cd227097bd93ba742d))
* **deps:** Update dependency Azure.Identity to 1.16.0 ([#15](https://github.com/eriklieben/ErikLieben.FA.ES/issues/15)) ([7a28c55](https://github.com/eriklieben/ErikLieben.FA.ES/commit/7a28c55973bf73ce47b81e9588857085520e057a))
* **deps:** Update dependency coverlet.collector to 6.0.4 ([#3](https://github.com/eriklieben/ErikLieben.FA.ES/issues/3)) ([ba2287a](https://github.com/eriklieben/ErikLieben.FA.ES/commit/ba2287adb02b8d5e2afc6ec013bf3da19804507c))
* **deps:** Update dependency Microsoft.Azure.Functions.Worker.Core to 2.1.0 ([#16](https://github.com/eriklieben/ErikLieben.FA.ES/issues/16)) ([d1f9052](https://github.com/eriklieben/ErikLieben.FA.ES/commit/d1f9052338c4ed018d0e4899ad0938a69aaca09f))
* **deps:** Update dependency Microsoft.Azure.WebJobs to 3.0.42 ([#4](https://github.com/eriklieben/ErikLieben.FA.ES/issues/4)) ([d630e9c](https://github.com/eriklieben/ErikLieben.FA.ES/commit/d630e9cc7ef38c139bb4a9e5d28a77600b0503bd))
* **deps:** Update dependency Microsoft.Bcl.AsyncInterfaces to 9.0.9 ([#11](https://github.com/eriklieben/ErikLieben.FA.ES/issues/11)) ([7818c37](https://github.com/eriklieben/ErikLieben.FA.ES/commit/7818c378a949d247f87edd37b097b0b9b921d5cd))
* **deps:** Update dependency Microsoft.Extensions.DependencyInjection to 9.0.9 ([#10](https://github.com/eriklieben/ErikLieben.FA.ES/issues/10)) ([952864e](https://github.com/eriklieben/ErikLieben.FA.ES/commit/952864e6f1484bd8375f7c837b729c3dd76833e6))
* **deps:** Update dependency Newtonsoft.Json to 13.0.4 ([#12](https://github.com/eriklieben/ErikLieben.FA.ES/issues/12)) ([4ad3064](https://github.com/eriklieben/ErikLieben.FA.ES/commit/4ad3064262e6e67219573e1c75edc6fc2de83681))
* **deps:** Update dependency semantic-release to 24.2.8 ([#13](https://github.com/eriklieben/ErikLieben.FA.ES/issues/13)) ([89c306b](https://github.com/eriklieben/ErikLieben.FA.ES/commit/89c306b1916d48b7653203d8b8d5ccc9cf0c9f67))
* **deps:** Update dependency Spectre.Console to 0.51.1 ([#8](https://github.com/eriklieben/ErikLieben.FA.ES/issues/8)) ([65360f4](https://github.com/eriklieben/ErikLieben.FA.ES/commit/65360f493e3eb824d0b44dd8bc55cd6519f73d42))
* **deps:** Update dependency Spectre.Console.Testing to 0.51.1 ([#5](https://github.com/eriklieben/ErikLieben.FA.ES/issues/5)) ([e970715](https://github.com/eriklieben/ErikLieben.FA.ES/commit/e970715bd38ef7b61f125472abdc53664eddc649))
* **deps:** Update dependency System.Formats.Asn1 to 9.0.9 ([#14](https://github.com/eriklieben/ErikLieben.FA.ES/issues/14)) ([e90e77a](https://github.com/eriklieben/ErikLieben.FA.ES/commit/e90e77ac14f0a8448d8b810381f2466df44b3533))
* **deps:** Update dependency xunit.runner.visualstudio to v3 ([#6](https://github.com/eriklieben/ErikLieben.FA.ES/issues/6)) ([8481e6f](https://github.com/eriklieben/ErikLieben.FA.ES/commit/8481e6f1f11f5bb37ea48f5d7c13934fa5c0fbd2))

## [1.0.1](https://github.com/eriklieben/ErikLieben.FA.ES/compare/v1.0.0...v1.0.1) (2025-08-27)

### 🐛 Bug fixes

* add analyzer for Aggregate-derived class should be partial ([ef2bc24](https://github.com/eriklieben/ErikLieben.FA.ES/commit/ef2bc242c8be0a78dc6b353ef3fbb98418b2ed43))

## 1.0.0 (2025-08-25)

### ✨ New features

* move code over to Github/ make public ([80ba991](https://github.com/eriklieben/ErikLieben.FA.ES/commit/80ba991ba0196edc62070411b02ae0e76cd2617d))

### 🧪 (Unit)test cases adjusted

* fix tests to work properly on linux ([fe9666f](https://github.com/eriklieben/ErikLieben.FA.ES/commit/fe9666fc17d505dc011258f2f192022c7ee3440e))
* fix tests to work properly on linux and bump versions of 2 deps ([99de094](https://github.com/eriklieben/ErikLieben.FA.ES/commit/99de094f50985e5c8f6968a3b6111452992d908f))
