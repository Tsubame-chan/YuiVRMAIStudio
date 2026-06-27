#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern "C" UIViewController* UnityGetGLViewController(void);

static NSString *const YuiPhotoPickerCancelled = @"__YUI_CANCELLED__";
static NSString *const YuiPhotoPickerErrorPrefix = @"__YUI_ERROR__:";

@interface YuiIOSPhotoPickerDelegate : NSObject <UIImagePickerControllerDelegate, UINavigationControllerDelegate>
@property(nonatomic, copy) NSString *callbackObjectName;
@end

static YuiIOSPhotoPickerDelegate *YuiPhotoPickerSharedDelegate;

static void YuiPhotoPickerSend(NSString *objectName, NSString *message)
{
    if (objectName.length == 0)
    {
        return;
    }
    UnitySendMessage(objectName.UTF8String, "OnIOSPhotoPickerResult", message.UTF8String);
}

static UIImage *YuiPhotoPickerNormalizeImage(UIImage *image)
{
    if (image.imageOrientation == UIImageOrientationUp)
    {
        return image;
    }

    UIGraphicsImageRendererFormat *format = [UIGraphicsImageRendererFormat defaultFormat];
    format.scale = image.scale;
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:image.size format:format];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext * _Nonnull context) {
        [image drawInRect:CGRectMake(0, 0, image.size.width, image.size.height)];
    }];
}

@implementation YuiIOSPhotoPickerDelegate

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker
{
    NSString *objectName = self.callbackObjectName;
    [picker dismissViewControllerAnimated:YES completion:^{
        YuiPhotoPickerSend(objectName, YuiPhotoPickerCancelled);
    }];
}

- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey,id> *)info
{
    NSString *objectName = self.callbackObjectName;
    UIImage *image = info[UIImagePickerControllerOriginalImage];
    if (image == nil)
    {
        [picker dismissViewControllerAnimated:YES completion:^{
            YuiPhotoPickerSend(objectName, [YuiPhotoPickerErrorPrefix stringByAppendingString:@"写真ライブラリから画像を取得できませんでした。"]);
        }];
        return;
    }

    UIImage *normalized = YuiPhotoPickerNormalizeImage(image);
    NSData *jpeg = UIImageJPEGRepresentation(normalized, 0.92);
    if (jpeg == nil || jpeg.length == 0)
    {
        [picker dismissViewControllerAnimated:YES completion:^{
            YuiPhotoPickerSend(objectName, [YuiPhotoPickerErrorPrefix stringByAppendingString:@"選択した画像をJPEGに変換できませんでした。"]);
        }];
        return;
    }

    NSString *filename = [NSString stringWithFormat:@"yui-picked-image-%@.jpg", NSUUID.UUID.UUIDString];
    NSString *path = [NSTemporaryDirectory() stringByAppendingPathComponent:filename];
    NSError *error = nil;
    if (![jpeg writeToFile:path options:NSDataWritingAtomic error:&error])
    {
        NSString *detail = error.localizedDescription ?: @"unknown error";
        [picker dismissViewControllerAnimated:YES completion:^{
            YuiPhotoPickerSend(objectName, [YuiPhotoPickerErrorPrefix stringByAppendingFormat:@"選択した画像を書き出せませんでした: %@", detail]);
        }];
        return;
    }

    [picker dismissViewControllerAnimated:YES completion:^{
        YuiPhotoPickerSend(objectName, path);
    }];
}

@end

extern "C" void YuiIOSPhotoPicker_OpenPhotoLibrary(const char *callbackObjectName)
{
    NSString *objectName = callbackObjectName != NULL
        ? [NSString stringWithUTF8String:callbackObjectName]
        : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        if (![UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypePhotoLibrary])
        {
            YuiPhotoPickerSend(objectName, [YuiPhotoPickerErrorPrefix stringByAppendingString:@"この端末では写真ライブラリを開けません。"]);
            return;
        }

        UIViewController *root = UnityGetGLViewController();
        if (root == nil)
        {
            YuiPhotoPickerSend(objectName, [YuiPhotoPickerErrorPrefix stringByAppendingString:@"Unityの表示コントローラを取得できませんでした。"]);
            return;
        }

        YuiPhotoPickerSharedDelegate = [YuiIOSPhotoPickerDelegate new];
        YuiPhotoPickerSharedDelegate.callbackObjectName = objectName;

        UIImagePickerController *picker = [UIImagePickerController new];
        picker.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
        picker.delegate = YuiPhotoPickerSharedDelegate;
        picker.allowsEditing = NO;

        UIViewController *presenting = root.presentedViewController ?: root;
        [presenting presentViewController:picker animated:YES completion:nil];
    });
}
