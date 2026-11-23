"""Assemble and read back the local-import ZIP from the Release build."""
from hashlib import sha256
import json
from pathlib import Path
import struct
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
manifest = json.loads((listing / "manifest.json").read_bytes())
project = ElementTree.parse(root / "PinABuildCost.csproj")
version = project.findtext(".//Version")
framework = project.findtext(".//TargetFramework")
if (manifest["name"] != "PinABuildCost" or manifest["version_number"] != version
        or manifest["website_url"] != "https://github.com/GKJ-Super-Fantasy-Kingdom/PinABuildCost"
        or not manifest["dependencies"] or len(manifest["description"]) > 250):
    raise ValueError("Invalid package manifest")

files = {
    "manifest.json": listing / "manifest.json",
    "icon.png": listing / "icon.png",
    "README.md": root / "README.md",
    "plugins/PinABuildCost.dll": root / "bin" / "Release" / framework / "PinABuildCost.dll",
}
output = root / "dist" / f"PinABuildCost-{version}.zip"
output.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=output.parent) as scratch:
    staged = Path(scratch) / output.name
    with ZipFile(staged, "w", ZIP_DEFLATED) as archive:
        for entry, source in files.items():
            archive.write(source, entry)
    with ZipFile(staged) as archive:
        if archive.testzip() is not None or sorted(archive.namelist()) != sorted(files):
            raise ValueError("Unexpected archive contents")
        for entry, source in files.items():
            if archive.read(entry) != source.read_bytes():
                raise ValueError(f"Readback mismatch in {entry}")
        png = archive.read("icon.png")
        if png[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", png[16:24]) != (256, 256):
            raise ValueError("Expected a 256 × 256 PNG")
        if archive.read("plugins/PinABuildCost.dll")[:2] != b"MZ":
            raise ValueError("Invalid DLL header")
    staged.replace(output)
print(output)
print("SHA256", sha256(output.read_bytes()).hexdigest())
