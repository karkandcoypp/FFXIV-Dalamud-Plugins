"""CraftProfile package/feed/sidecar verification using the shared verifier."""
import argparse, hashlib, sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'tools'))
from verify_release import verify
p=argparse.ArgumentParser();p.add_argument('--zip',required=True,type=Path);p.add_argument('--tag',required=True);p.add_argument('--repository',required=True);p.add_argument('--index',type=Path);p.add_argument('--checksum',type=Path)
a=p.parse_args()
if a.checksum and a.checksum.read_text().split()[0].lower()!=hashlib.sha256(a.zip.read_bytes()).hexdigest(): raise ValueError('Checksum mismatch')
verify(a.zip,Path(__file__).resolve().parents[1]/'CraftProfile/CraftProfile.json',a.repository,a.tag,a.index)
