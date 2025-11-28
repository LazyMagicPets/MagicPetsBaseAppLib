export const settings = {
  staticAssets: [
    // System (shared across all subtenants)
    { path: "/system/base/System/", cacheType: "PreCache", shared: true },
    { path: "/system/en-US/System/", cacheType: "PreCache", shared: true },
    { path: "/system/es-MX/System/", cacheType: "LazyCache", shared: true },

    // BaseApp (shared across all subtenants)
    { path: "/system/base/BaseApp/", cacheType: "PreCache", shared: true },
    { path: "/system/en-US/BaseApp/", cacheType: "PreCache", shared: true },
    { path: "/system/es-MX/BaseApp/", cacheType: "LazyCache", shared: true }
  ]
}