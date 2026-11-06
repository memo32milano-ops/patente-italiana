"""Replace the hi-res icon and the feature graphic of every store-listing language on Google Play,
then commit the edit (this sends the changes for review).
env: PLAY_JSON = service-account key (JSON text), PACKAGE
usage: python3 tools/play_upload_graphics.py store/icon512.png store/feature.jpg
"""
import os, sys, json
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

icon, feature = sys.argv[1], sys.argv[2]
pkg = os.environ.get('PACKAGE', 'com.memo.patentemondo')
creds = service_account.Credentials.from_service_account_info(
    json.loads(os.environ['PLAY_JSON']), scopes=['https://www.googleapis.com/auth/androidpublisher'])
ap = build('androidpublisher', 'v3', credentials=creds, cache_discovery=False)
edits = ap.edits()
eid = edits.insert(packageName=pkg, body={}).execute()['id']
langs = [l['language'] for l in edits.listings().list(packageName=pkg, editId=eid).execute().get('listings', [])]
print('listings:', len(langs), ' '.join(langs))
for lang in langs:
    for kind, path, mime in (('icon', icon, 'image/png'), ('featureGraphic', feature, 'image/jpeg')):
        edits.images().deleteall(packageName=pkg, editId=eid, language=lang, imageType=kind).execute()
        edits.images().upload(packageName=pkg, editId=eid, language=lang, imageType=kind,
                              media_body=MediaFileUpload(path, mimetype=mime)).execute()
    print('ok', lang)
# this app uses managed review: the edit is saved, then 'Send changes for review' is pressed in Play Console
res = edits.commit(packageName=pkg, editId=eid, changesNotSentForReview=True).execute()
print('committed edit', res.get('id'), '- saved in Play Console, ready to send for review')
