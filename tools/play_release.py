"""Publish a new version on Google Play in one edit:
- uploads the signed AAB (path in AAB), puts it on TRACK (default production) with release notes in it/en/ar,
- replaces the phone screenshots of the default listing with real app screenshots (store/play/shots/*.jpg),
- commits and sends the changes for review (falls back to 'not sent for review' only if Google asks for it).
"""
import os, json, glob, sys
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload
from googleapiclient.errors import HttpError

pkg = os.environ.get('PACKAGE', 'com.memo.patentemondo')
track = os.environ.get('TRACK', 'production')
aab = os.environ['AAB']
creds = service_account.Credentials.from_service_account_info(json.loads(os.environ['PLAY_JSON']), scopes=['https://www.googleapis.com/auth/androidpublisher'])
e = build('androidpublisher', 'v3', credentials=creds, cache_discovery=False).edits()
eid = e.insert(packageName=pkg, body={}).execute()['id']
details = e.details().get(packageName=pkg, editId=eid).execute()
default_lang = details.get('defaultLanguage', 'it-IT')
print('default language', default_lang)

b = e.bundles().upload(packageName=pkg, editId=eid, media_body=MediaFileUpload(aab, mimetype='application/octet-stream', resumable=True)).execute()
vc = b['versionCode']
print('bundle uploaded, versionCode', vc)

notes = [
    {'language': 'it-IT', 'text': "Nuova modalità guida 3D nelle vie di Milano con più situazioni (dossi, curve, strada sdrucciolevole, sorpasso, discesa), home più chiara, acquisti più affidabili con messaggi chiari, profilo con foto e pulsante Salva, correzioni e miglioramenti."},
    {'language': 'en-US', 'text': "New 3D driving mode in the streets of Milan with more situations (speed bumps, bends, slippery road, overtaking, descents), clearer home screen, more reliable purchases with clear messages, profile photo and Save button, fixes and improvements."},
    {'language': 'ar', 'text': "وضع سواقة 3D جديد في شوارع ميلانو بمواقف أكتر (مطبات، منحنيات، طريق زلق، تخطي، منحدرات)، صفحة رئيسية أوضح، مشتريات أكثر ثباتًا برسائل واضحة، صورة البروفايل وزرار حفظ، وتصليحات وتحسينات."},
]
listed = {l['language'] for l in e.listings().list(packageName=pkg, editId=eid).execute().get('listings', [])}
notes = [n for n in notes if n['language'] in listed or n['language'] == default_lang] or notes[:1]
e.tracks().update(packageName=pkg, editId=eid, track=track, body={
    'track': track,
    'releases': [{'name': os.environ.get('VERSION_NAME', '1.2.0') + ' (' + str(vc) + ')', 'versionCodes': [str(vc)], 'status': 'completed', 'releaseNotes': notes}]}).execute()
print('track', track, 'release with', len(notes), 'notes')

shots = sorted(glob.glob('store/play/shots/*.jpg'))
if shots:
    e.images().deleteall(packageName=pkg, editId=eid, language=default_lang, imageType='phoneScreenshots').execute()
    for s in shots:
        e.images().upload(packageName=pkg, editId=eid, language=default_lang, imageType='phoneScreenshots', media_body=MediaFileUpload(s, mimetype='image/jpeg')).execute()
    print('screenshots', len(shots), 'for', default_lang)

try:
    r = e.commit(packageName=pkg, editId=eid).execute()
    print('COMMITTED AND SENT FOR REVIEW', r.get('id'))
except HttpError as err:
    msg = str(err)
    print('commit:', msg[:600])
    if 'changesNotSentForReview' in msg:
        r = e.commit(packageName=pkg, editId=eid, changesNotSentForReview=True).execute()
        print('COMMITTED, NOT SENT FOR REVIEW (press "Send changes for review" in Play Console)', r.get('id'))
    else:
        sys.exit(1)
