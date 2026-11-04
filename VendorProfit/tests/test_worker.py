import copy
import datetime as dt
import importlib.util
import pathlib
import tempfile
import unittest

path = pathlib.Path(__file__).resolve().parents[1] / 'worker/vendorprofit_worker.py'
spec = importlib.util.spec_from_file_location('vendorprofit_worker', path)
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class WorkerTests(unittest.TestCase):
    def setUp(self):
        self.now = worker.utc()
        self.item = dict(Id=8144, Name='Clear Demimateria III', Npc=5000, CanHq=False, Stack=99, Equipment=False)
        self.request = dict(Schema=1, World=80, WorldName='Cerberus', Items=[self.item], IntervalSeconds=120,
            MaximumAgeMinutes=180, MaximumLot=50000, MinimumProfit=100, MinimumRoi=1)
        self.book = dict(itemID=8144, worldID=80, lastUploadTime=int(self.now.timestamp()*1000), listings=[
            dict(listingID='5629500000000001', pricePerUnit=4532, quantity=3, tax=679, total=13596,
                 hq=False, onMannequin=False, retainerName='Example')])

    def test_full_stack_tax_once_and_preserved_identity(self):
        found, unknown = worker.parse_book(self.book, self.item, self.request, self.now)
        self.assertFalse(unknown)
        self.assertEqual((found[0]['Quote']['Cost'], found[0]['Quote']['Revenue']), (14275,15000))
        self.assertEqual(found[0]['World'],80)
        self.assertEqual(found[0]['Quote']['Lot']['Id'],5629500000000001)

    def test_loss_and_roi_limits_excluded(self):
        self.request['MinimumRoi']=10
        self.assertEqual(worker.parse_book(self.book,self.item,self.request,self.now)[0],[])
        self.request['MinimumRoi']=1
        self.book['listings'][0].update(pricePerUnit=4771,quantity=2,tax=477,total=9542)
        self.assertEqual(worker.parse_book(self.book,self.item,self.request,self.now)[0],[])

    def test_wrong_world_stale_and_future_books_excluded(self):
        for change in [dict(worldID=83),dict(lastUploadTime=0),
                dict(lastUploadTime=int((self.now-dt.timedelta(days=1)).timestamp()*1000)),
                dict(lastUploadTime=int((self.now+dt.timedelta(days=1)).timestamp()*1000))]:
            book=copy.deepcopy(self.book);book.update(change)
            self.assertEqual(worker.parse_book(book,self.item,self.request,self.now),([],True))

    def test_listing_must_have_valid_identity_and_quantity(self):
        for field,value in [('listingID',None),('listingID','0'),('listingID',str(2**64)),('quantity',0),('quantity',-1),('quantity',True),('quantity',100),('onMannequin',True)]:
            book=copy.deepcopy(self.book);book['listings'][0][field]=value
            self.assertEqual(worker.parse_book(book,self.item,self.request,self.now)[0],[])

    def test_partial_batch_failure_keeps_other_books(self):
        items=[dict(self.item,Id=i) for i in range(1,22)]
        request=dict(self.request,Items=items)
        def fetch(url):
            if url.split('/')[1].split('?')[0]=='21':raise ValueError('failed book')
            books={}
            for i in range(1,21):
                book=copy.deepcopy(self.book);book['itemID']=i;books[str(i)]=book
            return dict(items=books)
        result=worker.scan(request,fetch=fetch,pause=lambda _:None)
        self.assertEqual((result['Checked'],result['Failed'],len(result['Suggestions'])),(21,1,20))
        self.assertEqual(result['Scope'],'External')

    def test_worker_stops_on_cooldown(self):
        def fetch(_):raise worker.Cooldown('wait')
        result=worker.scan(self.request,fetch=fetch,pause=lambda _:None)
        self.assertEqual((result['Unknown'],result['Suggestions']),(1,[]))

    def test_request_limits_and_atomic_publication(self):
        worker.validate(self.request)
        invalid=dict(self.request,Items=self.request['Items']*101)
        with self.assertRaises(ValueError):worker.validate(invalid)
        with tempfile.TemporaryDirectory() as directory:
            target=pathlib.Path(directory)/'result.json'
            worker.atomic(target,{'ready':True})
            self.assertEqual(target.read_text(),'{"ready": true}')
            self.assertFalse(target.with_suffix('.json.tmp').exists())

    def test_no_game_process_or_native_dependencies(self):
        source=path.read_text(encoding='utf8')
        for token in ('ctypes','ReadProcessMemory','SendInput','subprocess','Dalamud.dll','SendPurchaseRequest'):
            self.assertNotIn(token,source)

    def test_chaos_scope_labels_and_partial_failures(self):
        def fetch(url):
            if url=='data-centers':return [dict(name='Chaos',worlds=[80,83])]
            if url=='worlds':return [dict(id=80,name='Cerberus'),dict(id=83,name='Louisoix')]
            if url.startswith('83/'):raise ValueError('failed world')
            return self.book
        result=worker.scan(dict(self.request,Chaos=True),fetch=fetch,pause=lambda _:None)
        self.assertEqual(len(result['Servers']),2)
        self.assertEqual((len(result['Suggestions']),result['Suggestions'][0]['WorldName'],result['Failed']),(1,'Cerberus',1))

    def test_chaos_cooldown_stops_other_worlds(self):
        calls=[]
        def fetch(url):
            calls.append(url)
            if url=='data-centers':return [dict(name='Chaos',worlds=[80,83])]
            if url=='worlds':return [dict(id=80,name='Cerberus'),dict(id=83,name='Louisoix')]
            raise worker.Cooldown('stop')
        result=worker.scan(dict(self.request,Chaos=True),fetch=fetch,pause=lambda _:None)
        self.assertEqual(result['Unknown'],2)
        self.assertFalse(any(url.startswith('83/') for url in calls))

    def test_long_cooldown_propagated_to_background_interval(self):
        def fetch(_):raise worker.Cooldown('wait',3600)
        result=worker.scan(self.request,fetch=fetch,pause=lambda _:None)
        self.assertEqual(result['RetryAfterSeconds'],3600)
