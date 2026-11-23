"""Run the compiled checks in a separate process using the installed game's Mono runtime."""
import argparse
import ctypes as c
import os
from pathlib import Path
import struct
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--game", type=Path, default=Path(r"D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom"))
parser.add_argument("--profile", type=Path, default=Path(os.environ["APPDATA"]) /
                    "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/Daybook Dev")
args = parser.parse_args()
managed = args.game / "Super Fantasy Kingdom_Data/Managed"
runtime = args.game / "MonoBleedingEdge"
built = Path(__file__).resolve().parent / "bin/Release/netstandard2.1"
core = args.profile / "BepInEx/core"
assert struct.calcsize("P") == 8, "Use 64-bit Python"
os.environ["MONO_PATH"] = os.pathsep.join(map(str, [managed, built, core]))
dll_dir = os.add_dll_directory(str(runtime / "EmbedRuntime"))
mono = c.CDLL(str(runtime / "EmbedRuntime/mono-2.0-bdwgc.dll"))
ptr, string = c.c_void_p, c.c_char_p


def bind(name, result, *params):
    function = getattr(mono, name)
    function.argtypes, function.restype = params, result
    return function


bind("mono_set_dirs", None, string, string)(os.fsencode(managed), os.fsencode(runtime / "etc"))
bind("mono_set_assemblies_path", None, string)(os.fsencode(os.environ["MONO_PATH"]))
domain = bind("mono_jit_init_version", ptr, string, string)(b"PinChecks", b"v4.0.30319")
if not domain:
    sys.exit("Game Mono runtime initialization failed")
assembly = bind("mono_domain_assembly_open", ptr, ptr, string)(domain, os.fsencode(built / "PinChecks.dll"))
if not assembly:
    sys.exit("Build tests/PinChecks.csproj in Release first")
image = bind("mono_assembly_get_image", ptr, ptr)(assembly)
klass = bind("mono_class_from_name", ptr, ptr, string, string)(image, b"", b"Checks")
method = bind("mono_class_get_method_from_name", ptr, ptr, string, c.c_int)(klass, b"Run", 0)
error = ptr()
result = bind("mono_runtime_invoke", ptr, ptr, ptr, ptr, c.POINTER(ptr))(method, None, None, c.byref(error))
if error or not result:
    sys.exit("Unexpected managed exception in check runner")
value = bind("mono_object_unbox", ptr, ptr)(result)
sys.exit(c.cast(value, c.POINTER(c.c_int)).contents.value)
