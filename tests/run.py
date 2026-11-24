"""Run the compiled checks in a separate process using the installed game's Mono runtime."""
import argparse
import ctypes as c
import os
from pathlib import Path
import struct
import sys
import tempfile
from shutil import copyfile

parser = argparse.ArgumentParser()
parser.add_argument("--game", type=Path, default=Path(r"D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom"))
parser.add_argument("--profile", type=Path, default=Path(os.environ["APPDATA"]) /
                    "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/Daybook Dev")
parser.add_argument("--lone-bonfire", type=Path, help="Optional released LoneBonfire 1.1.1 DLL for the paired eligibility fixture")
parser.add_argument("--checkpoint-copy", type=Path, help="Optional copy of a kingdom .data checkpoint; tests write only in a temporary profile")
args = parser.parse_args()
if args.lone_bonfire:
    os.environ["PIN_TEST_LONE_BONFIRE"] = str(args.lone_bonfire.resolve(strict=True))
else:
    os.environ.pop("PIN_TEST_LONE_BONFIRE", None)
managed = args.game / "Super Fantasy Kingdom_Data/Managed"
runtime = args.game / "MonoBleedingEdge"
built = Path(__file__).resolve().parent / "bin/Release/netstandard2.1"
profile_copy = tempfile.TemporaryDirectory(dir=built)
os.environ["PIN_CHECKPOINT_FIXTURE"] = profile_copy.name
if args.checkpoint_copy:
    copyfile(args.checkpoint_copy, Path(profile_copy.name) / "original.data")
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
find_class = bind("mono_class_from_name", ptr, ptr, string, string)
find_method = bind("mono_class_get_method_from_name", ptr, ptr, string, c.c_int)
# Only this offline Mono process uses scene stand-ins for these engine calls.
scene = find_class(image, b"", b"SceneFixture")
compile_method = bind("mono_compile_method", ptr, ptr)
register = bind("mono_add_internal_call", None, string, ptr)
for native, stand_in, arguments in [
    ("Component::get_gameObject", "Owner", 1),
    ("GameObject::get_activeInHierarchy", "HierarchyActive", 1),
    ("GameObject::get_activeSelf", "Active", 1),
    ("Behaviour::get_isActiveAndEnabled", "Enabled", 1),
    ("GameObject::SetActive", "SetActive", 2),
    ("Object::Destroy", "Destroy", 2),
    ("Time::get_frameCount", "FrameCount", 0),
    ("Object::GetName", "Name", 1),
    ("Component::get_transform", "Location", 1),
    ("Transform::get_position_Injected", "Position", 2),
    ("JsonUtility::FromJsonInternal", "FromJson", 3),
    ("JsonUtility::ToJsonInternal", "ToJson", 2),
]:
    register(("UnityEngine." + native).encode(), compile_method(find_method(scene, stand_in.encode(), arguments)))
klass = find_class(image, b"", b"Checks")
method = find_method(klass, b"Run", 0)
error = ptr()
result = bind("mono_runtime_invoke", ptr, ptr, ptr, ptr, c.POINTER(ptr))(method, None, None, c.byref(error))
if error or not result:
    sys.exit("Unexpected managed exception in check runner")
value = bind("mono_object_unbox", ptr, ptr)(result)
sys.exit(c.cast(value, c.POINTER(c.c_int)).contents.value)
