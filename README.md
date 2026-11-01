# Patente Italiana

Unity 6 Android app to study the Italian driving licence B theory exam in 50 languages.

Cloud build: every push to `main` builds an APK and an AAB with GitHub Actions (see `.github/workflows/android.yml`).
Required repository secrets (Settings → Secrets and variables → Actions):
`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`.
