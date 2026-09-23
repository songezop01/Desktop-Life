"""Local, streaming FlyWire v783 importer. Never prints original neuron/edge rows."""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path
import time
import urllib.request

import pyarrow as pa
import pyarrow.compute as pc
import pyarrow.ipc as ipc

ANNOTATION_COMMIT = '8587524c1748ce5ef2080822a2fc890fc03bf597'
ANNOTATION_URL = f'https://raw.githubusercontent.com/flyconnectome/flywire_annotations/{ANNOTATION_COMMIT}/supplemental_files/Supplemental_file1_neuron_annotations.tsv'
EDGES_URL = 'https://zenodo.org/api/records/10676866/files/proofread_connections_783.feather/content'
EDGES_MD5 = 'f48f972d262323a102aed49af1396b8a'

class FlyWire783Adapter:
    def __init__(self, annotations, connections):
        self.annotations, self.connections = Path(annotations), Path(connections)

    def extract(self, output, side='left', threshold=5, max_neurons=5000, max_edges=100000):
        started = time.monotonic()
        with self.annotations.open(encoding='utf-8', newline='') as f:
            reader = csv.DictReader(f, delimiter='\t')
            required = {'root_id', 'cell_class', 'cell_type', 'side'}
            if not required.issubset(reader.fieldnames or []):
                raise ValueError('Unrecognized annotation schema')
            selected = {}
            for row in reader:
                if row['side'] != side or row['cell_class'] not in ('Kenyon_Cell', 'MBON', 'DAN'):
                    continue
                selected[int(row['root_id'])] = row
                if len(selected) > max_neurons:
                    raise ValueError('Subgraph too large: reduce neuron selection')
        if not selected:
            raise ValueError('No matching MB neurons')
        pairs = collections.Counter()
        scanned = 0
        with pa.memory_map(str(self.connections), 'r') as source:
            reader = ipc.open_file(source)
            columns = ('pre_pt_root_id', 'post_pt_root_id', 'syn_count', 'neuropil')
            if not set(columns).issubset(reader.schema.names):
                raise ValueError('Unrecognized FlyWire connections schema')
            for i in range(reader.num_record_batches):
                batch = reader.get_batch(i)
                pre, post = batch.column('pre_pt_root_id'), batch.column('post_pt_root_id')
                keep = pc.and_(pc.is_in(pre, value_set=pa.array(list(selected), type=pre.type)),
                               pc.is_in(post, value_set=pa.array(list(selected), type=post.type)))
                filtered = batch.filter(keep)
                for a, b, count in zip(filtered.column('pre_pt_root_id').to_pylist(),
                                       filtered.column('post_pt_root_id').to_pylist(), filtered.column('syn_count').to_pylist()):
                    if count <= 0:
                        raise ValueError('Invalid synapse count')
                    pairs[(a, b)] += count
                scanned += batch.num_rows
                if len(pairs) > 500000:
                    raise ValueError('Preprocessing edge dictionary exceeds budget')
        neurons = []
        for root, row in sorted(selected.items()):
            cell_type = row['cell_type'] or row['cell_class']
            # A reproducible engineering decoder, NOT an asserted biological behavior label.
            drive = int.from_bytes(hashlib.sha256(str(root).encode()).digest()[:4], 'little') % 7 if row['cell_class'] == 'MBON' else None
            neurons.append(dict(Id=str(root), Type=cell_type, Region='MB type-selected', Drive=drive))
        edges = [dict(Source=str(a), Target=str(b), OriginalWeight=count,
                      Plastic=selected[a]['cell_class']=='Kenyon_Cell' and selected[b]['cell_class']=='MBON')
                 for (a,b), count in sorted(pairs.items()) if count >= threshold]
        if len(edges)>max_edges or not edges:
            raise ValueError(f'Subgraph edge budget mismatch: {len(edges)}')
        graph = dict(Metadata=dict(Dataset='FlyWire FAFB v783', SubgraphName=f'MB-{side}-syn{threshold}',
                    Source=f'https://zenodo.org/records/10676866; annotations git {ANNOTATION_COMMIT}',
                    License='Connectivity CC-BY-4.0; annotations public research data, no separate repository license found',
                    Synthetic=False, SchemaVersion=1), Neurons=neurons, Edges=edges)
        output=Path(output); output.parent.mkdir(parents=True,exist_ok=True)
        temporary=output.with_suffix('.tmp'); temporary.write_text(json.dumps(graph,separators=(',',':')),encoding='utf-8');temporary.replace(output)
        summary=dict(neurons=len(neurons),edges=len(edges),plastic_edges=sum(e['Plastic'] for e in edges),
                     classes=dict(collections.Counter(r['cell_class'] for r in selected.values())),
                     source_rows_processed=scanned,seconds=round(time.monotonic()-started,2),
                     output_bytes=output.stat().st_size,annotation_commit=ANNOTATION_COMMIT,
                     note='Real topology; simplified unsigned propagation and arbitrary reproducible MBON-to-drive mapping')
        output.with_suffix('.summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
        return summary

def download(url,path):
    if not path.exists():
        temporary=path.with_suffix('.download');urllib.request.urlretrieve(url,temporary);temporary.replace(path)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--download',action='store_true');parser.add_argument('--raw',default='data/raw');parser.add_argument('--output',default='data/flywire-mb.json');parser.add_argument('--side',choices=['left','right'],default='left');parser.add_argument('--threshold',type=int,default=5)
    args=parser.parse_args();raw=Path(args.raw);raw.mkdir(parents=True,exist_ok=True)
    annotation=raw/'neuron_annotations.tsv';edges=raw/'proofread_connections_783.feather'
    if args.download:
        download(ANNOTATION_URL,annotation);download(EDGES_URL,edges)
    with edges.open('rb') as f:
        digest=hashlib.file_digest(f,'md5').hexdigest()
    if digest != EDGES_MD5:
        raise ValueError('Official connection file MD5 mismatch; partial or changed download')
    print(json.dumps(FlyWire783Adapter(annotation,edges).extract(args.output,args.side,args.threshold),indent=2))
