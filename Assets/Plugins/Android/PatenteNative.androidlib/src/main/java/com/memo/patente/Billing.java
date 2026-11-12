package com.memo.patente;

import android.app.Activity;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;
import com.android.billingclient.api.UnfetchedProduct;
import android.util.Log;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;

/**
 * Google Play Billing bridge.
 * Events are sent to Unity as strings through Native.Callback:
 *   "price|productId|formattedPrice"   price of a product
 *   "owned|productId|purchaseTimeMs|token"  product owned (purchase or restore)
 *   "pending|productId"                payment pending (e.g. cash at a shop)
 *   "cancel|productId"                 user cancelled
 *   "error|kind|detail"                any error; kind = unavailable (no Google Play billing on this phone / not signed in),
 *                                      noproduct (Play has nothing to sell for this id), other
 *   "ready|ok" / "ready|fail"          restore finished (fail = Play could not be asked; keep what we had)
 */
public class Billing {
    private static BillingClient client;
    private static Native.Callback events;
    private static final HashMap<String, ProductDetails> details = new HashMap<>();
    private static String[] inappIds = new String[0];
    private static String[] subsIds = new String[0];
    private static int setupCode = -100;   // last BillingClient setup result (-100 = not tried yet)

    private static void send(String s) { if (events != null) events.done(s); }

    public static void init(Activity activity, String inappCsv, String subsCsv, Native.Callback cb) {
        events = cb;
        inappIds = inappCsv.isEmpty() ? new String[0] : inappCsv.split(",");
        subsIds = subsCsv.isEmpty() ? new String[0] : subsCsv.split(",");
        if (client == null) {
            client = BillingClient.newBuilder(activity.getApplicationContext())
                    .setListener((result, purchases) -> {
                        if (result.getResponseCode() == BillingClient.BillingResponseCode.OK && purchases != null) {
                            for (Purchase p : purchases) handle(p);
                        } else if (result.getResponseCode() == BillingClient.BillingResponseCode.USER_CANCELED) {
                            send("cancel|");
                        } else if (result.getResponseCode() == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) {
                            restore();
                        } else {
                            send("error|other|" + result.getResponseCode() + " " + result.getDebugMessage());
                        }
                    })
                    .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
                    .enableAutoServiceReconnection()
                    .build();
        }
        connect();
    }

    private static void connect() {
        if (client.isReady()) { query(); return; }
        client.startConnection(new BillingClientStateListener() {
            @Override public void onBillingSetupFinished(BillingResult r) {
                setupCode = r.getResponseCode();
                if (r.getResponseCode() == BillingClient.BillingResponseCode.OK) query();
                else { Log.w("Billing", "setup " + r.getResponseCode() + " " + r.getDebugMessage()); send("ready|fail"); }
            }
            @Override public void onBillingServiceDisconnected() { /* reconnected on the next buy/restore */ }
        });
    }

    private static void query() {
        queryType(inappIds, BillingClient.ProductType.INAPP);
        queryType(subsIds, BillingClient.ProductType.SUBS);
        restore();
    }

    private static void queryType(String[] ids, String type) {
        if (ids.length == 0) return;
        List<QueryProductDetailsParams.Product> list = new ArrayList<>();
        for (String id : ids) list.add(QueryProductDetailsParams.Product.newBuilder().setProductId(id).setProductType(type).build());
        client.queryProductDetailsAsync(QueryProductDetailsParams.newBuilder().setProductList(list).build(), (r, res) -> {
            if (r.getResponseCode() != BillingClient.BillingResponseCode.OK || res == null) {
                Log.w("Billing", "query " + type + " " + r.getResponseCode() + " " + r.getDebugMessage());
                return;
            }
            for (UnfetchedProduct u : res.getUnfetchedProductList()) Log.w("Billing", "unfetched " + u.getProductId() + " " + u.getStatusCode());
            for (ProductDetails pd : res.getProductDetailsList()) {
                details.put(pd.getProductId(), pd);
                String price = "";
                if (pd.getOneTimePurchaseOfferDetails() != null) price = pd.getOneTimePurchaseOfferDetails().getFormattedPrice();
                else if (pd.getSubscriptionOfferDetails() != null && !pd.getSubscriptionOfferDetails().isEmpty()) {
                    List<ProductDetails.PricingPhase> ph = pd.getSubscriptionOfferDetails().get(0).getPricingPhases().getPricingPhaseList();
                    // last phase is the regular price (earlier phases may be a free trial)
                    price = ph.get(ph.size() - 1).getFormattedPrice();
                }
                send("price|" + pd.getProductId() + "|" + price);
                // Play lists only the offers this user can still get: a first phase at 0 = free trial available
                if (pd.getSubscriptionOfferDetails() != null) {
                    boolean trial = false;
                    for (ProductDetails.SubscriptionOfferDetails o : pd.getSubscriptionOfferDetails()) {
                        List<ProductDetails.PricingPhase> ph = o.getPricingPhases().getPricingPhaseList();
                        if (!ph.isEmpty() && ph.get(0).getPriceAmountMicros() == 0) trial = true;
                    }
                    send("trial|" + pd.getProductId() + "|" + (trial ? "1" : "0"));
                }
            }
        });
    }

    public static void restore() {
        if (client == null) return;
        if (!client.isReady()) { connect(); return; }
        final String[] types = { BillingClient.ProductType.INAPP, BillingClient.ProductType.SUBS };
        final int[] pending = { types.length };
        final boolean[] ok = { true };
        for (String t : types) {
            client.queryPurchasesAsync(QueryPurchasesParams.newBuilder().setProductType(t).build(), (r, purchases) -> {
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) ok[0] = false;
                else if (purchases != null) for (Purchase p : purchases) handle(p);
                pending[0]--;
                if (pending[0] == 0) send(ok[0] ? "ready|ok" : "ready|fail");
            });
        }
    }

    /** Consumes a one-time purchase (used when the 3-month pass has expired, so it can be bought again). */
    public static void consume(String token) {
        if (client == null || !client.isReady() || token == null || token.isEmpty()) return;
        client.consumeAsync(com.android.billingclient.api.ConsumeParams.newBuilder().setPurchaseToken(token).build(), (r, t) -> { });
    }

    private static void handle(Purchase p) {
        if (p.getPurchaseState() == Purchase.PurchaseState.PENDING) { for (String id : p.getProducts()) send("pending|" + id); return; }
        if (p.getPurchaseState() != Purchase.PurchaseState.PURCHASED) return;
        for (String id : p.getProducts()) send("owned|" + id + "|" + p.getPurchaseTime() + "|" + p.getPurchaseToken());
        if (!p.isAcknowledged()) {
            client.acknowledgePurchase(AcknowledgePurchaseParams.newBuilder().setPurchaseToken(p.getPurchaseToken()).build(), r -> { });
        }
    }

    public static void buy(Activity activity, String productId) {
        if (client == null) { send("error|unavailable|not started"); return; }
        if (!client.isReady()) { connect(); send("error|unavailable|" + setupCode); return; }
        ProductDetails pd = details.get(productId);
        if (pd == null) { query(); send("error|noproduct|" + productId); return; }
        BillingFlowParams.ProductDetailsParams.Builder b = BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(pd);
        List<ProductDetails.SubscriptionOfferDetails> offers = pd.getSubscriptionOfferDetails();
        if (offers != null && !offers.isEmpty()) {
            // prefer the offer that starts with a free trial, else the base plan
            ProductDetails.SubscriptionOfferDetails pick = offers.get(0);
            for (ProductDetails.SubscriptionOfferDetails o : offers) {
                List<ProductDetails.PricingPhase> ph = o.getPricingPhases().getPricingPhaseList();
                if (!ph.isEmpty() && ph.get(0).getPriceAmountMicros() == 0) { pick = o; break; }
            }
            b.setOfferToken(pick.getOfferToken());
        }
        BillingFlowParams params = BillingFlowParams.newBuilder().setProductDetailsParamsList(Collections.singletonList(b.build())).build();
        activity.runOnUiThread(() -> {
            BillingResult r = client.launchBillingFlow(activity, params);
            if (r.getResponseCode() == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) restore();
            else if (r.getResponseCode() == BillingClient.BillingResponseCode.BILLING_UNAVAILABLE) send("error|unavailable|" + r.getResponseCode());
            else if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) send("error|other|launch " + r.getResponseCode());
        });
    }
}
