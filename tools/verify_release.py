"""Validate installable packages and temporary index metadata for every repository plugin."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import tempfile
import struct
import zipfile
from update_repository import update


def require_dll(package, name):
    data = package.read(name)

    def need(condition):
        if not condition:
            raise ValueError('Invalid PE/CLR metadata: ' + name)

    def u16(at):
        return struct.unpack_from('<H', data, at)[0]

    def u32(at):
        return struct.unpack_from('<I', data, at)[0]

    need(len(data) >= 64 and data[:2] == b'MZ')
    pe = u32(60)
    need(64 <= pe <= len(data) - 26 and data[pe:pe+4] == b'PE\0\0' and u16(pe+22) & 0x2000)
    optional = pe + 24
    section_count = u16(pe+6)
    section_start = optional + u16(pe+20)
    magic = u16(optional)
    need(magic in (0x10b, 0x20b) and section_count > 0 and section_start + section_count*40 <= len(data))
    directories = optional + (96 if magic == 0x10b else 112)
    cli = directories + 14*8
    need(cli+8 <= section_start and u32(directories-4) >= 15 and u32(cli+4) >= 72)
    sections = [struct.unpack_from('<IIII', data, section_start+i*40+8) for i in range(section_count)]

    def file_offset(rva, length):
        for virtual_size, start, raw_size, raw in sections:
            delta = rva - start
            if rva and 0 <= delta and delta+length <= min(raw_size, virtual_size or raw_size) and raw+delta+length <= len(data):
                return raw + delta
        raise ValueError('Unmapped CLR data: ' + name)

    header = file_offset(u32(cli), 72)
    need(u32(header) >= 72)
    size = u32(header+12)
    need(size >= 20)
    root = file_offset(u32(header+8), size)
    end = root + size
    need(data[root:root+4] == b'BSJB')
    version_size = u32(root+12)
    cursor = root + 16 + version_size
    need(version_size > 0 and version_size % 4 == 0 and cursor+4 <= end)
    count = u16(cursor+2)
    cursor += 4
    streams = {}
    for _ in range(count):
        need(cursor+9 <= end)
        offset, length = u32(cursor), u32(cursor+4)
        name_end = data.find(b'\0', cursor+8, min(cursor+40, end))
        need(name_end >= 0 and offset+length <= size)
        key = data[cursor+8:name_end]
        need(key and key not in streams)
        streams[key] = (root+offset, length)
        cursor = root + ((name_end+1-root+3) & ~3)
    need(all(at >= cursor for at, _ in streams.values()))
    need(b'#Strings' in streams and streams[b'#Strings'][1] > 0)
    tables, length = streams.get(b'#~', streams.get(b'#-', (0, 0)))
    need(length >= 28)
    valid = struct.unpack_from('<Q', data, tables+8)[0]
    assembly = 1 << 32
    need(valid & assembly and 24+4*valid.bit_count() <= length)
    need(u32(tables+24+4*(valid & (assembly-1)).bit_count()) == 1)


def verify(archive, manifest, repository, tag, index=None):
    expected = json.loads(Path(manifest).read_text(encoding='utf-8-sig'))
    name = expected['InternalName']
    with tempfile.TemporaryDirectory() as folder:
        candidate = Path(folder) / 'pluginmaster.json'
        candidate.write_text('[]', encoding='utf-8')
        update(candidate, manifest, repository, tag)  # Validates plugin identity and tag/version agreement.
        entry = json.loads(candidate.read_text(encoding='utf-8'))[0]
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(set(names)) or any('..' in PurePosixPath(n).parts or PurePosixPath(n).is_absolute() or '\\' in n or ':' in n for n in names):
            raise ValueError('Unsafe or duplicate archive entries')
        if any(e.file_size > 100_000_000 for e in package.infolist()) or package.testzip() is not None:
            raise ValueError('Corrupt or oversized archive')
        actual = json.loads(package.read(name + '.json'))
        for key in ('InternalName', 'AssemblyVersion', 'DalamudApiLevel'):
            if actual.get(key) != expected.get(key):
                raise ValueError('Package manifest mismatch: ' + key)
        if not actual.get('DalamudApiLevel'):
            raise ValueError('Missing plugin API')
        require_dll(package, name + '.dll')
        if name == 'HuntRoute':
            allowed_icons = {entry['IconUrl']}
            if repository.casefold() == 'karkandcoypp/xiv':
                allowed_icons.add('https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/HuntRoute/icon.png')
            if expected.get('IconUrl') not in allowed_icons or actual.get('IconUrl') not in allowed_icons:
                raise ValueError('Missing or mismatched Hunt Route package IconUrl')
            require_dll(package, 'KamiToolKit.dll')
    if index:
        entries = json.loads(Path(index).read_text(encoding='utf-8-sig'))
        own = [e for e in entries if e.get('InternalName') == name]
        if len(own) != 1 or any(own[0].get(k) != entry[k] for k in ('AssemblyVersion', 'DalamudApiLevel', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')):
            raise ValueError('Package / repository index mismatch')
    print('PASS: ' + name + ' ' + expected['AssemblyVersion'] + ' ZIP, manifest, DLL and index; SHA256 ' + hashlib.sha256(Path(archive).read_bytes()).hexdigest())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--zip', required=True, type=Path)
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--index', type=Path)
    args = parser.parse_args()
    verify(args.zip, args.manifest, args.repository, args.tag, args.index)
