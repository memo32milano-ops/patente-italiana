"""Update short descriptions (store/short_desc.json) and the it-IT phone screenshots (store/1..5_*.jpg),
then commit the edit WITHOUT sending it for review (press 'Send changes for review' in Play Console)."""
import os, json, glob
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload
pkg = os.environ.get('PACKAGE', 'com.memo.patentemondo')
creds = service_account.Credentials.from_service_account_info(json.loads(os.environ['PLAY_JSON']), scopes=['https://www.googleapis.com/auth/androidpublisher'])
e = build('androidpublisher', 'v3', credentials=creds, cache_discovery=False).edits()
eid = e.insert(packageName=pkg, body={}).execute()['id']
short = json.load(open('store/short_desc.json', encoding='utf-8'))
for lang, text in short.items():
    assert len(text) <= 80, lang
    e.listings().patch(packageName=pkg, editId=eid, language=lang, body={'shortDescription': text}).execute()
print('short descriptions:', len(short))
shots = sorted(glob.glob('store/[1-9]_*.jpg'))
e.images().deleteall(packageName=pkg, editId=eid, language='it-IT', imageType='phoneScreenshots').execute()
for s in shots:
    e.images().upload(packageName=pkg, editId=eid, language='it-IT', imageType='phoneScreenshots', media_body=MediaFileUpload(s, mimetype='image/jpeg')).execute()
print('screenshots:', shots)
print('committed', e.commit(packageName=pkg, editId=eid, changesNotSentForReview=True).execute().get('id'))
