"""Minimal PE/CLR metadata fixture for publication tests without a compiler."""
import struct


def managed_dll():
    dll = bytearray(1024)
    dll[:2] = b'MZ'
    struct.pack_into('<I', dll, 60, 64)
    dll[64:68] = b'PE\0\0'
    struct.pack_into('<H', dll, 70, 1)
    struct.pack_into('<HH', dll, 84, 240, 0x2000)
    struct.pack_into('<H', dll, 88, 0x20b)
    struct.pack_into('<I', dll, 196, 16)
    struct.pack_into('<II', dll, 312, 0x2000, 72)
    struct.pack_into('<IIII', dll, 336, 512, 0x2000, 512, 512)
    struct.pack_into('<IHHII', dll, 512, 72, 2, 5, 0x2080, 124)
    root = 640
    dll[root:root+4] = b'BSJB'
    struct.pack_into('<I', dll, root+12, 4)
    dll[root+16:root+20] = b'v4\0\0'
    struct.pack_into('<H', dll, root+22, 2)
    struct.pack_into('<II', dll, root+24, 56, 50)
    dll[root+32:root+36] = b'#~\0\0'
    struct.pack_into('<II', dll, root+36, 108, 16)
    dll[root+44:root+52] = b'#Strings'
    struct.pack_into('<Q', dll, root+64, 1 << 32)
    struct.pack_into('<I', dll, root+80, 1)
    return bytes(dll)
