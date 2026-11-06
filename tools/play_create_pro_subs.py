"""Create the professional-licence subscriptions (pro_monthly with 3-day free trial, pro_yearly) on Google Play,
copying regions/settings from premium_monthly. Prices: EUR consumer prices (VAT included), converted by Google for other regions."""
import os, json, copy
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
pkg = 'com.memo.patentemondo'
creds = service_account.Credentials.from_service_account_info(json.loads(os.environ['PLAY_JSON']), scopes=['https://www.googleapis.com/auth/androidpublisher'])
m = build('androidpublisher', 'v3', credentials=creds, cache_discovery=False).monetization()
S = m.subscriptions()
tpl = S.get(packageName=pkg, productId='premium_monthly').execute()
bp0 = tpl['basePlans'][0]
regions = [r['regionCode'] for r in bp0['regionalConfigs']]
print('template base plan', json.dumps({k: v for k, v in bp0.items() if k != 'regionalConfigs'})[:600])
offers = S.basePlans().offers().list(packageName=pkg, productId='premium_monthly', basePlanId=bp0['basePlanId']).execute().get('subscriptionOffers', [])
print('template offers', json.dumps(offers)[:1500])

def prices(eur):
    units, nanos = int(eur), int(round((eur - int(eur)) * 1e9))
    r = m.convertRegionPrices(packageName=pkg, body={'price': {'currencyCode': 'EUR', 'units': str(units), 'nanos': nanos}}).execute()
    conv = r['convertedRegionPrices']
    global RV; RV = r.get('regionVersion', {}).get('version', '2022/02')
    out = []
    for code in regions:
        if code not in conv: continue
        p = conv[code]['price']
        if code == 'IT': p = {'currencyCode': 'EUR', 'units': str(units), 'nanos': nanos}
        out.append({'regionCode': code, 'newSubscriberAvailability': True, 'price': p})
    return out, r.get('convertedOtherRegionsPrice')

L = {
 'it-IT': ('Patenti professionali C/CE e CQC', 'Patente C/CE, CQC Merci e CQC Persone: tutti i quiz ministeriali tradotti e spiegati, simulazioni d\'esame illimitate.'),
 'en-US': ('Professional licences C/CE and CQC', 'Licence C/CE, CQC Goods and CQC Passengers: all ministry questions translated and explained, unlimited exam simulations.'),
 'ar': ('الرخص المهنية C/CE و CQC', 'رخصة C/CE و CQC بضائع و CQC ركاب: كل الأسئلة مترجمة ومشروحة، وامتحانات تجريبية بلا حدود.'),
}
def listings():
    return [{'languageCode': k, 'title': t, 'benefits': [], 'description': d[:80]} for k, (t, d) in L.items()]

for pid, period, eur, trial in (('pro_monthly', 'P1M', 3.99, True), ('pro_yearly', 'P1Y', 24.99, False)):
    try:
        S.get(packageName=pkg, productId=pid).execute(); print(pid, 'already exists'); continue
    except HttpError as e:
        if e.resp.status != 404: raise
    rc, other = prices(eur)
    print('regions version', RV)
    bpid = 'monthly' if period == 'P1M' else 'yearly'
    bp = {'basePlanId': bpid, 'autoRenewingBasePlanType': copy.deepcopy(bp0['autoRenewingBasePlanType']), 'regionalConfigs': rc}
    bp['autoRenewingBasePlanType']['billingPeriodDuration'] = period
    bp['autoRenewingBasePlanType'].pop('legacyCompatibleSubscriptionOfferId', None)
    if 'otherRegionsConfig' in bp0 and other:
        bp['otherRegionsConfig'] = {'usdPrice': other['usdPrice'], 'eurPrice': other['eurPrice'], 'newSubscriberAvailability': True}
    body = {'packageName': pkg, 'productId': pid, 'listings': listings(), 'basePlans': [bp], 'taxAndComplianceSettings': tpl.get('taxAndComplianceSettings')}
    S.create(packageName=pkg, productId=pid, body=body, regionsVersion_version=RV).execute()
    S.basePlans().activate(packageName=pkg, productId=pid, basePlanId=bpid, body={}).execute()
    print('created + activated', pid, len(rc), 'regions')
    if trial:
        ob = {'packageName': pkg, 'productId': pid, 'basePlanId': bpid, 'offerId': 'free-trial-3d',
              'phases': [{'recurrenceCount': 1, 'duration': 'P3D', 'regionalConfigs': [{'regionCode': r['regionCode'], 'free': {}} for r in rc],
                          **({'otherRegionsConfig': {'free': {}}} if 'otherRegionsConfig' in bp else {})}],
              'targeting': {'acquisitionRule': {'scope': {'thisSubscription': {}}}},
              'regionalConfigs': [{'regionCode': r['regionCode'], 'newSubscriberAvailability': True} for r in rc]}
        if 'otherRegionsConfig' in bp: ob['otherRegionsConfig'] = {'otherRegionsNewSubscriberAvailability': True}
        S.basePlans().offers().create(packageName=pkg, productId=pid, basePlanId=bpid, offerId='free-trial-3d', body=ob, regionsVersion_version=RV).execute()
        S.basePlans().offers().activate(packageName=pkg, productId=pid, basePlanId=bpid, offerId='free-trial-3d', body={}).execute()
        print('trial offer active')
for pid in ('pro_monthly', 'pro_yearly'):
    s = S.get(packageName=pkg, productId=pid).execute()
    for b in s['basePlans']:
        it = [r for r in b['regionalConfigs'] if r['regionCode'] == 'IT']
        print(pid, b['basePlanId'], b.get('state'), it)
