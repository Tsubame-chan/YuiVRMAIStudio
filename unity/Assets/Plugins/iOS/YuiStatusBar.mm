#import "UnityAppController.h"
#import "PluginBase/UnityViewControllerListener.h"
#import "UI/UnityAppController+ViewHandling.h"
#import "UI/UnityViewControllerBase.h"
#import "UI/UnityViewControllerBase+iOS.h"

// Use Unity's supported controller factory, keeping orientation handling intact.
// Yui's minimum iOS version is 16.2, where the default controller handles fixed
// orientations as well as autorotation.
static BOOL yuiGalleryHidden = NO;
@interface YuiViewController : UnityDefaultViewController
@end
@implementation YuiViewController
- (BOOL)prefersStatusBarHidden { return yuiGalleryHidden; }
- (UIStatusBarStyle)preferredStatusBarStyle { return UIStatusBarStyleLightContent; }
@end

@interface YuiAppController : UnityAppController
@end
@implementation YuiAppController
- (UIViewController*)createUnityViewControllerDefault
{
    YuiViewController* controller = [[YuiViewController alloc] initShouldHandleFixedOrientation:YES];
    controller.notificationDelegate = [[UnityViewControllerNotificationsDefaultSender alloc] init];
    return controller;
}
@end
IMPL_APP_CONTROLLER_SUBCLASS(YuiAppController)

extern "C" void YuiStatusBarSetHidden(int hidden)
{
    yuiGalleryHidden = hidden != 0;
    [GetAppController().rootViewController setNeedsStatusBarAppearanceUpdate];
}
