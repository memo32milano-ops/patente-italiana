"""Export everything the Play reviewers look at (read-only, the edit is deleted at the end)."""
import os, json
from google.oauth2 import service_account
from googleapiclient.discovery import build
pkg = os.environ.get('PACKAGE', 'com.memo.patentemondo')
creds = service_account.Credentials.from_service_account_info(json.loads(os.environ['PLAY_JSON']), scopes=['https://www.googleapis.com/auth/androidpublisher'])
ap = build('androidpublisher', 'v3', credentials=creds, cache_discovery=False)
e = ap.edits(); eid = e.insert(packageName=pkg, body={}).execute()['id']
out = {'details': e.details().get(packageName=pkg, editId=eid).execute(), 'listings': {}, 'images': {}, 'tracks': e.tracks().list(packageName=pkg, editId=eid).execute()}
for l in e.listings().list(packageName=pkg, editId=eid).execute().get('listings', []):
    out['listings'][l['language']] = l
    imgs = {}
    for t in ('phoneScreenshots', 'sevenInchScreenshots', 'tenInchScreenshots', 'featureGraphic', 'icon', 'tvBanner'):
        r = e.images().list(packageName=pkg, editId=eid, language=l['language'], imageType=t).execute().get('images', [])
        if r: imgs[t] = [{'url': i['url'], 'sha1': i.get('sha1')} for i in r]
    out['images'][l['language']] = imgs
try:
    out['inapp'] = ap.monetization().onetimeproducts().list(packageName=pkg).execute()
except Exception as x: out['inapp_err'] = str(x)
try:
    out['subs'] = ap.monetization().subscriptions().list(packageName=pkg).execute()
except Exception as x: out['subs_err'] = str(x)
e.delete(packageName=pkg, editId=eid).execute()
os.makedirs('audit', exist_ok=True)
json.dump(out, open('audit/play.json', 'w'), ensure_ascii=False, indent=1)
print('langs', len(out['listings']))
