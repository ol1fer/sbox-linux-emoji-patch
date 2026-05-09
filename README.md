Patches s&box's bundled `Topten.RichTextKit.dll` to fix emoji rendering on Linux/Proton.

Context: https://github.com/Facepunch/sbox-public/issues/10779

Prerequisites:
- .NET 10 SDK
- ilspycmd
- git, patch
- s&box installed

On Arch you can grab everything except ilspycmd and s&box (lol) with:

```
sudo pacman -S dotnet-sdk git patch
```

On other distros find the equivalent for your package manager.

1. Install .NET 10 SDK from https://dotnet.microsoft.com/download (skip if you used the pacman line above)

2. Install ilspycmd:

```
dotnet tool install -g ilspycmd
```

3. Clone this repo and cd into it:

```
git clone https://github.com/ol1fer/sbox-linux-emoji-patch.git
cd sbox-linux-emoji-patch
```

4. Decompile s&box's bundled RichTextKit. Replace the path with your sbox install path if it's different:

```
ilspycmd ~/.steam/steam/steamapps/common/sbox/bin/managed/Topten.RichTextKit.dll -p -o build
```

5. Apply the patches and copy in the project files:

```
patch -p1 -d build < patches/Utf32Utils.patch
patch -p1 -d build < patches/FontRun.patch
cp DefaultCharacterMatcher.cs build/Topten.RichTextKit/
cp Topten.RichTextKit.csproj build/
cp W10Emoji.ttf build/
```

6. Build, passing the path to your sbox bin/managed folder:

```
dotnet build build/Topten.RichTextKit.csproj -c Release -p:SboxBinManaged=/home/oliver/.steam/steam/steamapps/common/sbox/bin/managed
```

7. Back up the original DLL and replace it with the patched one:

```
cp ~/.steam/steam/steamapps/common/sbox/bin/managed/Topten.RichTextKit.dll ~/.steam/steam/steamapps/common/sbox/bin/managed/Topten.RichTextKit.dll.bak
cp build/bin/Release/Topten.RichTextKit.dll ~/.steam/steam/steamapps/common/sbox/bin/managed/
```

Done. Launch s&box, emoji should now render correctly. To revert, copy the `.bak` back over the patched DLL or verify game files through Steam.
