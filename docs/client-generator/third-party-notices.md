# Third-party software in the Salep build tool

The `Salep.ClientGenerator` package bundles these private build-time dependencies, including their resource assemblies:

| Component | Version | Project | License |
| --- | --- | --- | --- |
| Microsoft.CodeAnalysis.Common | 5.9.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.CSharp | 5.9.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.Workspaces.Common | 5.9.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.9.0 | https://github.com/dotnet/roslyn | MIT |
| Humanizer.Core | 2.14.1 | https://github.com/Humanizr/Humanizer | MIT |
| System.Composition.AttributedModel, Convention, Hosting, Runtime, TypedParts | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.12 | https://github.com/dotnet/runtime | MIT |

Copyright (c) Microsoft Corporation. All rights reserved.
Copyright (c) .NET Foundation and Contributors. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

The upstream notices distributed in these NuGet packages are included unchanged under `licenses/`: `roslyn-ThirdPartyNotices.rtf` (shared by the Roslyn packages) and `runtime-THIRD-PARTY-NOTICES.txt` (from FileSystemGlobbing), `composition-THIRD-PARTY-NOTICES.txt` (from System.Composition), and `humanizer-LICENSE.txt`. Update this inventory and the copied notices whenever the bundled dependency versions change. The upstream runtime notice file describes the wider runtime distribution; its inclusion does not imply all listed components are bundled in Salep.

The Salep generator and bundled Salep GraphQL Parser are covered by the included `LICENSE` file. Generated applications do not acquire runtime references to these private tooling assemblies.
