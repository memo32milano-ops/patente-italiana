"""Print the review state of the app, its version and its purchases (read only)."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import asc_store as a
app = '6831966088'
for s in a.pages(f'/v1/apps/{app}/reviewSubmissions', {'filter[platform]': 'IOS'}):
    print('submission', s['attributes'].get('state'), s['attributes'].get('submittedDate'))
    for it in a.pages(f"/v1/reviewSubmissions/{s['id']}/items"):
        print('   item', it['attributes'].get('state'))
for v in a.pages(f'/v1/apps/{app}/appStoreVersions', {'filter[platform]': 'IOS'}):
    print('version', v['attributes'].get('versionString'), v['attributes'].get('appStoreState'))
    b = a.call('GET', f"/v1/appStoreVersions/{v['id']}/build") or {}
    print('   build', ((b.get('data') or {}).get('attributes') or {}).get('version'))
for i in a.pages(f'/v1/apps/{app}/inAppPurchasesV2'):
    print('iap', i['attributes']['productId'], i['attributes'].get('state'))
for g in a.pages(f'/v1/apps/{app}/subscriptionGroups'):
    for s in a.pages(f"/v1/subscriptionGroups/{g['id']}/subscriptions"):
        print('sub', s['attributes']['productId'], s['attributes'].get('state'))
