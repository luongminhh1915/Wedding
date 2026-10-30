export interface FacebookSdk {
  init(options: { appId: string; cookie: boolean; xfbml: boolean; version: string }): void;
  login(
    callback: (response: { authResponse?: { accessToken: string } | null }) => void,
    options: { scope: string },
  ): void;
}

declare global {
  interface Window {
    FB?: FacebookSdk;
    fbAsyncInit?: () => void;
  }
}

let loadingSdk: Promise<FacebookSdk> | null = null;
let initializedAppId: string | null = null;
let initializedVersion: string | null = null;

function initializeFacebook(appId: string, version: string): FacebookSdk {
  const sdk = window.FB;
  if (!sdk) throw new Error("Facebook SDK chưa sẵn sàng.");
  if (initializedAppId && (initializedAppId !== appId || initializedVersion !== version)) {
    throw new Error("Facebook SDK đã được khởi tạo với cấu hình khác. Hãy tải lại trang.");
  }
  if (!initializedAppId) {
    sdk.init({ appId, cookie: false, xfbml: false, version });
    initializedAppId = appId;
    initializedVersion = version;
  }
  return sdk;
}

export function loadFacebookSdk(appId: string, version: string): Promise<FacebookSdk> {
  if (window.FB) {
    try { return Promise.resolve(initializeFacebook(appId, version)); }
    catch (error) { return Promise.reject(error); }
  }
  if (loadingSdk) return loadingSdk.then(() => initializeFacebook(appId, version));

  loadingSdk = new Promise<FacebookSdk>((resolve, reject) => {
    let settled = false;
    const finish = (error?: Error) => {
      if (settled) return;
      settled = true;
      window.clearTimeout(timeout);
      if (window.fbAsyncInit === onReady) window.fbAsyncInit = undefined;
      if (error) {
        loadingSdk = null;
        reject(error);
        return;
      }
      try { resolve(initializeFacebook(appId, version)); }
      catch (failure) { loadingSdk = null; reject(failure); }
    };
    const onReady = () => finish();
    const timeout = window.setTimeout(() => finish(new Error("Không tải được Facebook SDK. Hãy kiểm tra kết nối rồi thử lại.")), 15000);
    window.fbAsyncInit = onReady;

    let script = document.getElementById("facebook-jssdk") as HTMLScriptElement | null;
    if (script && window.FB) { finish(); return; }
    if (!script) {
      script = document.createElement("script");
      script.id = "facebook-jssdk";
      script.async = true;
      script.defer = true;
      script.src = "https://connect.facebook.net/en_US/sdk.js?xfbml=0&version=" + encodeURIComponent(version);
    }
    script.addEventListener("error", () => finish(new Error("Không thể tải Facebook SDK. Vui lòng thử lại sau.")), { once: true });
    script.addEventListener("load", () => { if (window.FB) finish(); }, { once: true });
    if (!script.isConnected) document.head.appendChild(script);
  });
  return loadingSdk;
}
