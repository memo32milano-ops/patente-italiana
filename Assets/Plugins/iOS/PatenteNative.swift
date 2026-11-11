// iOS side of Patente Italiana: speech, on-device translation, sharing, photo picker,
// daily reminder and App Store purchases (StoreKit 2). Mirrors the Android plugin (com.memo.patente.*).
// Every result goes back to C# through one C callback: (requestId, text), always on the main thread.

import Foundation
import UIKit
import AVFoundation
import StoreKit
import UserNotifications
import PhotosUI
#if canImport(MLKitTranslate)
import MLKitTranslate
#endif

public typealias PACallback = @convention(c) (Int32, UnsafePointer<CChar>?) -> Void
private var paCallback: PACallback?

private func send(_ id: Int32, _ text: String?) {
    DispatchQueue.main.async {
        guard let cb = paCallback else { return }
        if let t = text { t.withCString { cb(id, $0) } } else { cb(id, nil) }
    }
}

private func str(_ p: UnsafePointer<CChar>?) -> String { p.map { String(cString: $0) } ?? "" }

private func topController() -> UIViewController? {
    var vc = UIApplication.shared.connectedScenes.compactMap { ($0 as? UIWindowScene)?.keyWindow }.first?.rootViewController
    while let p = vc?.presentedViewController { vc = p }
    return vc
}

@_cdecl("pa_set_callback")
public func pa_set_callback(_ cb: PACallback?) { paCallback = cb }

@_cdecl("pa_locale")
public func pa_locale() -> UnsafeMutablePointer<CChar>? {
    let lang = Locale.preferredLanguages.first ?? "en"
    return strdup(lang)
}

// ------------------------------------------------------------------ speech
private final class Speech: NSObject, AVSpeechSynthesizerDelegate {
    static let shared = Speech()
    let synth = AVSpeechSynthesizer()
    var pending: Int32 = -1
    override init() { super.init(); synth.delegate = self }
    func speechSynthesizer(_ s: AVSpeechSynthesizer, didFinish u: AVSpeechUtterance) { done() }
    func speechSynthesizer(_ s: AVSpeechSynthesizer, didCancel u: AVSpeechUtterance) { done() }
    func done() { let id = pending; pending = -1; if id >= 0 { send(id, "") } }
}

@_cdecl("pa_speak")
public func pa_speak(_ text: UnsafePointer<CChar>?, _ rate: Float, _ id: Int32) {
    let t = str(text)
    DispatchQueue.main.async {
        let sp = Speech.shared
        if sp.synth.isSpeaking { sp.synth.stopSpeaking(at: .immediate) }
        try? AVAudioSession.sharedInstance().setCategory(.playback, mode: .spokenAudio, options: [.duckOthers])
        try? AVAudioSession.sharedInstance().setActive(true)
        let u = AVSpeechUtterance(string: t)
        u.voice = AVSpeechSynthesisVoice(language: "it-IT")
        // Android rate 1.0 = normal; AVSpeech default is 0.5
        u.rate = max(AVSpeechUtteranceMinimumSpeechRate, min(AVSpeechUtteranceMaximumSpeechRate, AVSpeechUtteranceDefaultSpeechRate * rate))
        sp.pending = id
        sp.synth.speak(u)
    }
}

@_cdecl("pa_stop")
public func pa_stop() {
    DispatchQueue.main.async { Speech.shared.synth.stopSpeaking(at: .immediate) }
}

@_cdecl("pa_voice_available")
public func pa_voice_available() -> Bool {
    AVSpeechSynthesisVoice.speechVoices().contains { $0.language.hasPrefix("it") }
}

// ------------------------------------------------------------------ on-device translation (Google ML Kit, free, offline after the model download)
#if canImport(MLKitTranslate)
private var translators: [String: Translator] = [:]

@_cdecl("pa_translate")
public func pa_translate(_ source: UnsafePointer<CChar>?, _ text: UnsafePointer<CChar>?, _ target: UnsafePointer<CChar>?, _ id: Int32) {
    let s = str(source), t = str(text), g = str(target)
    DispatchQueue.main.async {
        let src = TranslateLanguage(rawValue: s), dst = TranslateLanguage(rawValue: g == "he" ? "he" : g)
        guard TranslateLanguage.allLanguages().contains(src), TranslateLanguage.allLanguages().contains(dst) else { send(id, nil); return }
        let key = s + ">" + g
        let tr = translators[key] ?? Translator.translator(options: TranslatorOptions(sourceLanguage: src, targetLanguage: dst))
        translators[key] = tr
        tr.downloadModelIfNeeded(with: ModelDownloadConditions(allowsCellularAccess: true, allowsBackgroundDownloading: true)) { err in
            if err != nil { send(id, nil); return }
            tr.translate(t) { res, e in send(id, e == nil ? res : nil) }
        }
    }
}
#else
// simulator screenshot builds have no ML Kit (it ships for devices only)
@_cdecl("pa_translate")
public func pa_translate(_ source: UnsafePointer<CChar>?, _ text: UnsafePointer<CChar>?, _ target: UnsafePointer<CChar>?, _ id: Int32) { send(id, nil) }
#endif

// ------------------------------------------------------------------ share / settings
@_cdecl("pa_share")
public func pa_share(_ text: UnsafePointer<CChar>?) {
    let t = str(text)
    DispatchQueue.main.async {
        guard let vc = topController() else { return }
        let a = UIActivityViewController(activityItems: [t], applicationActivities: nil)
        a.popoverPresentationController?.sourceView = vc.view
        a.popoverPresentationController?.sourceRect = CGRect(x: vc.view.bounds.midX, y: vc.view.bounds.maxY - 40, width: 1, height: 1)
        vc.present(a, animated: true)
    }
}

@_cdecl("pa_open_settings")
public func pa_open_settings() {
    DispatchQueue.main.async {
        if let u = URL(string: UIApplication.openSettingsURLString) { UIApplication.shared.open(u) }
    }
}

// ------------------------------------------------------------------ photo picker (96x96 JPEG written to a temp file)
private final class Picker: NSObject, PHPickerViewControllerDelegate {
    static let shared = Picker()
    var id: Int32 = -1
    func picker(_ picker: PHPickerViewController, didFinishPicking results: [PHPickerResult]) {
        picker.dismiss(animated: true)
        let rid = id
        guard let p = results.first?.itemProvider, p.canLoadObject(ofClass: UIImage.self) else { send(rid, ""); return }
        p.loadObject(ofClass: UIImage.self) { obj, _ in
            guard let img = obj as? UIImage else { send(rid, ""); return }
            let side = min(img.size.width, img.size.height)
            let r = UIGraphicsImageRenderer(size: CGSize(width: 96, height: 96))
            let small = r.image { _ in
                let k = 96 / side
                img.draw(in: CGRect(x: (96 - img.size.width * k) / 2, y: (96 - img.size.height * k) / 2, width: img.size.width * k, height: img.size.height * k))
            }
            let url = FileManager.default.temporaryDirectory.appendingPathComponent("pa_photo.jpg")
            do { try small.jpegData(compressionQuality: 0.8)?.write(to: url); send(rid, url.path) } catch { send(rid, "") }
        }
    }
}

@_cdecl("pa_pick_photo")
public func pa_pick_photo(_ id: Int32) {
    DispatchQueue.main.async {
        var cfg = PHPickerConfiguration()
        cfg.filter = .images
        cfg.selectionLimit = 1
        let p = PHPickerViewController(configuration: cfg)
        Picker.shared.id = id
        p.delegate = Picker.shared
        topController()?.present(p, animated: true)
    }
}

// ------------------------------------------------------------------ daily reminder
@_cdecl("pa_reminder")
public func pa_reminder(_ on: Bool, _ hour: Int32, _ title: UnsafePointer<CChar>?, _ text: UnsafePointer<CChar>?) {
    let t = str(title), b = str(text)
    let c = UNUserNotificationCenter.current()
    c.removePendingNotificationRequests(withIdentifiers: ["pa_daily"])
    guard on else { return }
    c.requestAuthorization(options: [.alert, .sound, .badge]) { ok, _ in
        guard ok else { return }
        let n = UNMutableNotificationContent()
        n.title = t; n.body = b; n.sound = .default
        var d = DateComponents(); d.hour = Int(hour); d.minute = 0
        c.add(UNNotificationRequest(identifier: "pa_daily", content: n, trigger: UNCalendarNotificationTrigger(dateMatching: d, repeats: true)))
    }
}

// ------------------------------------------------------------------ App Store purchases (StoreKit 2)
// Events sent with id 0, same format as the Android billing plugin:
// price|id|text   owned|id|purchaseMillis|transactionId   pending   cancel   error|msg   ready|ok
private var products: [String: Product] = [:]
private var updatesTask: Task<Void, Never>?

private func ev(_ s: String) { send(0, s) }

private func millis(_ d: Date) -> String { String(Int64(d.timeIntervalSince1970 * 1000)) }

private func entitlements() async {
    for await r in Transaction.currentEntitlements {
        if case .verified(let t) = r, t.revocationDate == nil {
            if let exp = t.expirationDate, exp < Date() { continue }
            ev("owned|\(t.productID)|\(millis(t.purchaseDate))|\(t.id)")
        }
    }
    ev("ready|ok")
}

@_cdecl("pa_store_init")
public func pa_store_init(_ ids: UnsafePointer<CChar>?) {
    let list = str(ids).split(separator: ",").map(String.init)
    updatesTask?.cancel()
    updatesTask = Task.detached {
        for await r in Transaction.updates {
            if case .verified(let t) = r {
                if t.revocationDate == nil { ev("owned|\(t.productID)|\(millis(t.purchaseDate))|\(t.id)") }
                await t.finish()
            }
        }
    }
    Task {
        do {
            for p in try await Product.products(for: list) { products[p.id] = p; ev("price|\(p.id)|\(p.displayPrice)") }
        } catch { ev("error|products \(error.localizedDescription)") }
        await entitlements()
    }
}

@_cdecl("pa_store_buy")
public func pa_store_buy(_ pid: UnsafePointer<CChar>?) {
    let id = str(pid)
    Task {
        guard let p = products[id] else { ev("error|unknown product \(id)"); return }
        do {
            switch try await p.purchase() {
            case .success(let v):
                if case .verified(let t) = v {
                    ev("owned|\(t.productID)|\(millis(t.purchaseDate))|\(t.id)")
                    await t.finish()
                } else { ev("error|unverified") }
            case .pending: ev("pending")
            case .userCancelled: ev("cancel")
            @unknown default: ev("cancel")
            }
        } catch { ev("error|\(error.localizedDescription)") }
    }
}

@_cdecl("pa_store_restore")
public func pa_store_restore() {
    Task {
        try? await AppStore.sync()
        await entitlements()
    }
}
