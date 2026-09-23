import csv
import tempfile
import unittest
from pathlib import Path
import pyarrow as pa
import pyarrow.feather as feather
from prepare_flywire import FlyWire783Adapter

class AdapterTests(unittest.TestCase):
    def test_schema_filter_direction_aggregation_and_mask(self):
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory)
            with (p/'annotations.tsv').open('w',newline='') as f:
                writer=csv.writer(f,delimiter='\t');writer.writerow(['root_id','cell_class','cell_type','side'])
                writer.writerows([[1,'Kenyon_Cell','KC_ab','left'],[2,'MBON','MBON01','left'],[3,'DAN','PAM01','left'],[4,'Kenyon_Cell','KC_ab','right']])
            feather.write_feather(pa.table({'pre_pt_root_id':[1,1,3,4],'post_pt_root_id':[2,2,1,2],'syn_count':[3,4,6,100],'neuropil':['MB_L','CA_L','MB_L','MB_R']}),p/'edges.feather')
            result=FlyWire783Adapter(p/'annotations.tsv',p/'edges.feather').extract(p/'out.json')
            self.assertEqual(result['neurons'],3);self.assertEqual(result['edges'],2);self.assertEqual(result['plastic_edges'],1)
            import json
            graph=json.loads((p/'out.json').read_text());self.assertEqual(graph['Edges'][0]['OriginalWeight'],7)
    def test_wrong_schema_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory);(p/'wrong.tsv').write_text('wrong\nvalue\n')
            with self.assertRaises(ValueError):FlyWire783Adapter(p/'wrong.tsv',p/'none').extract(p/'out')
if __name__=='__main__':unittest.main()
