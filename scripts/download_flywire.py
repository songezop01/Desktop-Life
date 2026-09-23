"""Resume the official file in four bounded HTTP ranges, then verify its checksum."""
import concurrent.futures
import hashlib
from pathlib import Path
import shutil
import urllib.request

URL='https://zenodo.org/records/10676866/files/proofread_connections_783.feather?download=1'
SIZE=852022274
EXPECTED='f48f972d262323a102aed49af1396b8a'
path=Path('data/raw/proofread_connections_783.feather')
path.parent.mkdir(parents=True,exist_ok=True)
offset=path.stat().st_size if path.exists() else 0
if offset>SIZE:raise RuntimeError('Existing file is larger than official size')

def fetch(segment):
    start,end=segment
    part=path.with_name(f'{path.name}.{start}-{end}.part')
    if part.exists() and part.stat().st_size==end-start+1:return part
    request=urllib.request.Request(URL,headers={'Range':f'bytes={start}-{end}'})
    with urllib.request.urlopen(request,timeout=90) as response:
        if response.status!=206 or response.headers.get('Content-Range')!=f'bytes {start}-{end}/{SIZE}':
            raise RuntimeError('Server did not honor exact requested range')
        with part.open('wb') as f:shutil.copyfileobj(response,f,1024*1024)
    if part.stat().st_size!=end-start+1:raise RuntimeError('Incomplete range')
    return part

if offset<SIZE:
    chunk=(SIZE-offset+3)//4
    ranges=[(start,min(SIZE-1,start+chunk-1)) for start in range(offset,SIZE,chunk)]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:parts=list(pool.map(fetch,ranges))
    with path.open('ab') as destination:
        for part in parts:
            with part.open('rb') as source:shutil.copyfileobj(source,destination,1024*1024)
with path.open('rb') as f:actual=hashlib.file_digest(f,'md5').hexdigest()
if actual!=EXPECTED:raise RuntimeError('Official MD5 mismatch')
print(f'Official FlyWire file verified: {SIZE} bytes; MD5 {actual}')
