# Third-Party License Notices

CSP-Analyzer's distributed builds include the following third-party components with their respective licenses. This file documents the licenses for all dependencies bundled in the packaged application.

## .NET Dependencies

- **Avalonia** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia/11.2.3

- **Avalonia.Desktop** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia.Desktop/11.2.3

- **Avalonia.Themes.Fluent** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia.Themes.Fluent/11.2.3

- **Avalonia.Fonts.Inter** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia.Fonts.Inter/11.2.3

- **Avalonia.Controls.DataGrid** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia.Controls.DataGrid/11.2.3

- **CommunityToolkit.Mvvm** (8.4.2) — MIT License  
  https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2

- **LiveChartsCore.SkiaSharpView.Avalonia** (2.0.5) — MIT License  
  https://www.nuget.org/packages/LiveChartsCore.SkiaSharpView.Avalonia/2.0.5

- **ClosedXML** (0.105.0) — MIT License  
  https://www.nuget.org/packages/ClosedXML/0.105.0

- **PDFsharp** (6.2.4) — MIT License  
  https://www.nuget.org/packages/PDFsharp/6.2.4

## Transitive .NET Dependencies

The packages above are pulled in directly via `<PackageReference>`. A self-contained `dotnet publish` also bundles the following transitive dependencies (verified by publishing `dotnet/CspAnalyzer.Desktop` for `linux-x64` and inspecting the resulting `.deps.json` and output directory):

- **SkiaSharp** (2.88.9, + native binaries) — MIT License  
  https://www.nuget.org/packages/SkiaSharp/2.88.9

- **SkiaSharp.HarfBuzz** (2.88.9) — MIT License  
  https://www.nuget.org/packages/SkiaSharp.HarfBuzz/2.88.9

- **HarfBuzzSharp** (7.3.0.3, + native binaries) — MIT License  
  https://www.nuget.org/packages/HarfBuzzSharp/7.3.0.3

- **DocumentFormat.OpenXml** (3.1.1) — MIT License  
  https://www.nuget.org/packages/DocumentFormat.OpenXml/3.1.1

- **DocumentFormat.OpenXml.Framework** (3.1.1) — MIT License  
  https://www.nuget.org/packages/DocumentFormat.OpenXml.Framework/3.1.1

- **SixLabors.Fonts** (1.0.0) — Apache License 2.0  
  https://www.nuget.org/packages/SixLabors.Fonts/1.0.0

- **ExcelNumberFormat** (1.1.0) — MIT License  
  https://www.nuget.org/packages/ExcelNumberFormat/1.1.0

- **ClosedXML.Parser** (2.0.0) — MIT License  
  https://www.nuget.org/packages/ClosedXML.Parser/2.0.0

- **LiveChartsCore** (2.0.5) — MIT License  
  https://www.nuget.org/packages/LiveChartsCore/2.0.5

- **LiveChartsCore.SkiaSharpView** (2.0.5) — MIT License  
  https://www.nuget.org/packages/LiveChartsCore.SkiaSharpView/2.0.5

- **RBush.Signed** (4.0.0) — MIT License  
  https://www.nuget.org/packages/RBush.Signed/4.0.0

- **MicroCom.Runtime** (0.11.0) — MIT License  
  https://www.nuget.org/packages/MicroCom.Runtime/0.11.0

- **Tmds.DBus.Protocol** (0.20.0) — MIT License  
  https://www.nuget.org/packages/Tmds.DBus.Protocol/0.20.0

- **Microsoft.Extensions.DependencyInjection.Abstractions** (8.0.2) — MIT License  
  https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/8.0.2

- **Microsoft.Extensions.Logging.Abstractions** (8.0.3) — MIT License  
  https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/8.0.3

- **System.IO.Packaging** (8.0.1) — MIT License  
  https://www.nuget.org/packages/System.IO.Packaging/8.0.1

- **System.IO.Pipelines** (8.0.0) — MIT License  
  https://www.nuget.org/packages/System.IO.Pipelines/8.0.0

- **System.Security.Cryptography.Pkcs** (8.0.1) — MIT License  
  https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/8.0.1

- **Avalonia platform-backend packages** (11.2.3) — MIT License  
  https://www.nuget.org/packages/Avalonia.Skia/11.2.3  
  Additional AvaloniaUI-project packages pulled in transitively at the same version and license as the Avalonia entries above: `Avalonia.Skia`, `Avalonia.Win32`, `Avalonia.X11`, `Avalonia.Native`, `Avalonia.FreeDesktop`, `Avalonia.Remote.Protocol` (rendering/windowing backends for Windows, X11, macOS/Cocoa, and Linux D-Bus integration respectively — only the backend(s) matching the target platform are loaded at runtime, but all are copied into a self-contained publish).

- **.NET 8 Runtime** — MIT License  
  https://github.com/dotnet/runtime  
  
  A self-contained publish bundles the .NET 8 runtime itself (`libcoreclr.so`/equivalent, `System.*` and `Microsoft.*` base class library assemblies, `hostfxr`/`hostpolicy`) so the application runs without a pre-installed .NET runtime on the target machine. The .NET runtime is developed by Microsoft and the .NET Foundation and is licensed under the MIT License.

## Python Dependencies

- **numpy** (2.5.1) — BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0  
  https://pypi.org/project/numpy/2.5.1/

- **scipy** (1.18.0) — BSD License  
  https://pypi.org/project/scipy/1.18.0/

- **scikit-image** (0.26.0) — BSD License  
  https://pypi.org/project/scikit-image/0.26.0/

- **scikit-learn** (1.9.0) — BSD-3-Clause License  
  https://pypi.org/project/scikit-learn/1.9.0/

## Build Tooling

- **PyInstaller** (6.21.0) — GPL-2.0-or-later WITH bootloader-exception  
  https://github.com/pyinstaller/pyinstaller  
  
  The PyInstaller bootloader is licensed under the GPL with an explicit exception permitting the compiled bootloader and related files to be linked into and distributed with any program without GPL restrictions. This exception allows applications built with PyInstaller to be distributed under any license, including proprietary licenses, as long as the built application complies with the licenses of its own dependencies.

## Bundled Fonts

- **Inter font** (bundled via Avalonia.Fonts.Inter) — SIL Open Font License, Version 1.1  
  https://github.com/rsms/inter/blob/master/LICENSE.txt  
  
  The Inter font is a typeface designed by Rasmus Andersson and licensed under the SIL OFL 1.1, which permits use, modification, and redistribution as long as the font is not sold individually and the license terms are included with the font.

- **DejaVu Sans** (Assets/Fonts/DejaVuSans.ttf) — Bitstream Vera / Arev / Public Domain  
  https://dejavu-fonts.github.io/License.html  
  
  DejaVu is derived from Bitstream Vera (Copyright © 2003 Bitstream Inc.) and Arev fonts (Copyright © 2006 Tavmjong Bah). DejaVu modifications are placed in the public domain. The combined work is available under the Bitstream Vera License with modifications tracked by the DejaVu project.
