const MINIMUM_SUBPACKAGE_VERSION = '2.1.0';

function compareVersion(left, right) {
    const leftParts = left.split('.');
    const rightParts = right.split('.');
    const length = Math.max(leftParts.length, rightParts.length);
    for (let index = 0; index < length; index++) {
        const leftPart = parseInt(leftParts[index] || '0', 10);
        const rightPart = parseInt(rightParts[index] || '0', 10);
        if (leftPart !== rightPart) {
            return leftPart > rightPart ? 1 : -1;
        }
    }
    return 0;
}

const systemInfo = wx.getSystemInfoSync();
if (compareVersion(systemInfo.SDKVersion || '0.0.0', MINIMUM_SUBPACKAGE_VERSION) >= 0) {
    try {
        const plugin = requirePlugin('MinigameLoading');
        const loadingManager = plugin.default || plugin;
        GameGlobal.LoadingManager = loadingManager;
        GameGlobal.minigameLoadingPromise = loadingManager.create({
            images: [
                {
                    src: 'images/minigame-loading-cover.jpg',
                },
            ],
            contextType: 'webgl',
            contextAttributes: {},
            showLoading: true,
            scaleMode: loadingManager.ScaleMode.NO_BORDER,
            designWidth: 563,
            designHeight: 1218,
        }).catch((error) => {
            console.error('[MinigameLoading] create failed:', error);
        });
    }
    catch (error) {
        console.error('[MinigameLoading] requirePlugin failed:', error);
    }
}

GameGlobal.destroyMinigameLoading = function () {
    if (!GameGlobal.LoadingManager) {
        return;
    }

    const loadingManager = GameGlobal.LoadingManager;
    GameGlobal.LoadingManager = null;
    Promise.resolve(GameGlobal.minigameLoadingPromise)
        .then(() => loadingManager.destroy())
        .catch((error) => {
            console.error('[MinigameLoading] destroy failed:', error);
        });
};
