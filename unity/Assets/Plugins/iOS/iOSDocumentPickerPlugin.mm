#import <Foundation/Foundation.h>
#import <PhotosUI/PhotosUI.h>
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern "C" UIViewController* UnityGetGLViewController(void);

static NSString *const YuiDocumentPickerCancelled = @"__YUI_CANCELLED__";
static NSString *const YuiDocumentPickerErrorPrefix = @"__YUI_ERROR__:";

@interface YuiIOSDocumentPickerDelegate : NSObject <UIDocumentPickerDelegate>
@property(nonatomic, copy) NSString *callbackObjectName;
@property(nonatomic, copy) NSString *mode;
@end

static YuiIOSDocumentPickerDelegate *YuiDocumentPickerSharedDelegate;

@interface YuiIOSPhotoPickerDelegate : NSObject <PHPickerViewControllerDelegate>
@property(nonatomic, copy) NSString *callbackObjectName;
@end

static YuiIOSPhotoPickerDelegate *YuiPhotoPickerSharedDelegate;

static void YuiDocumentPickerSend(NSString *objectName, NSString *message)
{
    if (objectName.length == 0)
    {
        return;
    }
    UnitySendMessage(objectName.UTF8String, "OnIOSDocumentPickerResult", message.UTF8String);
}

static NSString *YuiDocumentPickerSafeExtension(NSURL *url, NSString *mode)
{
    NSString *extension = url.pathExtension.lowercaseString;
    if (extension.length > 0)
    {
        return extension;
    }
    return [mode isEqualToString:@"vrm"] ? @"vrm" : @"jpg";
}

static NSString *YuiDocumentPickerTargetRoot(NSString *mode)
{
    if ([mode isEqualToString:@"vrm"])
    {
        NSArray<NSString *> *paths = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
        return [[paths firstObject] stringByAppendingPathComponent:@"YuiImportedFiles/VRM"];
    }

    return [NSTemporaryDirectory() stringByAppendingPathComponent:@"YuiPickedFiles/Image"];
}

static NSString *YuiDocumentPickerCopyURL(NSURL *url, NSString *mode, NSError **error)
{
    NSString *root = YuiDocumentPickerTargetRoot(mode);
    NSFileManager *fileManager = NSFileManager.defaultManager;
    if (![fileManager createDirectoryAtPath:root withIntermediateDirectories:YES attributes:nil error:error])
    {
        return nil;
    }

    NSString *extension = YuiDocumentPickerSafeExtension(url, mode);
    NSString *prefix = [mode isEqualToString:@"vrm"] ? @"yui-imported-vrm" : @"yui-picked-image";
    NSString *filename = [NSString stringWithFormat:@"%@-%@.%@", prefix, NSUUID.UUID.UUIDString, extension];
    NSString *target = [root stringByAppendingPathComponent:filename];

    if ([fileManager fileExistsAtPath:target])
    {
        [fileManager removeItemAtPath:target error:nil];
    }

    if (![fileManager copyItemAtURL:url toURL:[NSURL fileURLWithPath:target] error:error])
    {
        return nil;
    }
    return target;
}

static NSString *YuiDocumentPickerImageExtension(NSItemProvider *provider)
{
    for (NSString *identifier in provider.registeredTypeIdentifiers)
    {
        UTType *type = [UTType typeWithIdentifier:identifier];
        if (type != nil && [type conformsToType:UTTypeImage])
        {
            NSString *extension = type.preferredFilenameExtension.lowercaseString;
            if (extension.length > 0)
            {
                return extension;
            }
        }
    }

    return @"jpg";
}

static NSString *YuiDocumentPickerCopyImageData(NSData *data, NSString *extension, NSError **error)
{
    if (data.length == 0)
    {
        if (error != nil)
        {
            *error = [NSError errorWithDomain:@"YuiIOSPhotoPicker" code:1 userInfo:@{NSLocalizedDescriptionKey: @"画像データが空です。"}];
        }
        return nil;
    }

    NSString *root = YuiDocumentPickerTargetRoot(@"image");
    NSFileManager *fileManager = NSFileManager.defaultManager;
    if (![fileManager createDirectoryAtPath:root withIntermediateDirectories:YES attributes:nil error:error])
    {
        return nil;
    }

    NSString *safeExtension = extension.length > 0 ? extension : @"jpg";
    NSString *filename = [NSString stringWithFormat:@"yui-picked-photo-%@.%@", NSUUID.UUID.UUIDString, safeExtension];
    NSString *target = [root stringByAppendingPathComponent:filename];
    return [data writeToFile:target options:NSDataWritingAtomic error:error] ? target : nil;
}

@implementation YuiIOSDocumentPickerDelegate

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    YuiDocumentPickerSend(self.callbackObjectName, YuiDocumentPickerCancelled);
    YuiDocumentPickerSharedDelegate = nil;
}

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    NSURL *url = urls.firstObject;
    if (url == nil)
    {
        YuiDocumentPickerSend(self.callbackObjectName, [YuiDocumentPickerErrorPrefix stringByAppendingString:@"ファイルが選択されませんでした。"]);
        YuiDocumentPickerSharedDelegate = nil;
        return;
    }

    BOOL scoped = [url startAccessingSecurityScopedResource];
    NSError *error = nil;
    NSString *path = YuiDocumentPickerCopyURL(url, self.mode ?: @"image", &error);
    if (scoped)
    {
        [url stopAccessingSecurityScopedResource];
    }

    if (path.length == 0)
    {
        NSString *detail = error.localizedDescription ?: @"unknown error";
        YuiDocumentPickerSend(self.callbackObjectName, [YuiDocumentPickerErrorPrefix stringByAppendingFormat:@"選択したファイルをコピーできませんでした: %@", detail]);
        YuiDocumentPickerSharedDelegate = nil;
        return;
    }

    YuiDocumentPickerSend(self.callbackObjectName, path);
    YuiDocumentPickerSharedDelegate = nil;
}

@end

@implementation YuiIOSPhotoPickerDelegate

- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results API_AVAILABLE(ios(14))
{
    [picker dismissViewControllerAnimated:YES completion:nil];

    PHPickerResult *result = results.firstObject;
    if (result == nil)
    {
        YuiDocumentPickerSend(self.callbackObjectName, YuiDocumentPickerCancelled);
        YuiPhotoPickerSharedDelegate = nil;
        return;
    }

    NSItemProvider *provider = result.itemProvider;
    if (![provider hasItemConformingToTypeIdentifier:UTTypeImage.identifier])
    {
        YuiDocumentPickerSend(self.callbackObjectName, [YuiDocumentPickerErrorPrefix stringByAppendingString:@"画像ファイルを取得できませんでした。"]);
        YuiPhotoPickerSharedDelegate = nil;
        return;
    }

    NSString *extension = YuiDocumentPickerImageExtension(provider);
    NSString *callbackObjectName = self.callbackObjectName;
    [provider loadDataRepresentationForTypeIdentifier:UTTypeImage.identifier completionHandler:^(NSData * _Nullable data, NSError * _Nullable error) {
        NSString *message = nil;
        if (data.length > 0)
        {
            NSError *writeError = nil;
            NSString *path = YuiDocumentPickerCopyImageData(data, extension, &writeError);
            if (path.length > 0)
            {
                message = path;
            }
            else
            {
                NSString *detail = writeError.localizedDescription ?: @"unknown error";
                message = [YuiDocumentPickerErrorPrefix stringByAppendingFormat:@"選択した写真をコピーできませんでした: %@", detail];
            }
        }
        else
        {
            NSString *detail = error.localizedDescription ?: @"unknown error";
            message = [YuiDocumentPickerErrorPrefix stringByAppendingFormat:@"写真データを読み込めませんでした: %@", detail];
        }

        dispatch_async(dispatch_get_main_queue(), ^{
            YuiDocumentPickerSend(callbackObjectName, message);
            YuiPhotoPickerSharedDelegate = nil;
        });
    }];
}

@end

static void YuiDocumentPicker_OpenPhoto(NSString *objectName)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *root = UnityGetGLViewController();
        if (root == nil)
        {
            YuiDocumentPickerSend(objectName, [YuiDocumentPickerErrorPrefix stringByAppendingString:@"Unityの表示コントローラを取得できませんでした。"]);
            return;
        }

        if (@available(iOS 14, *))
        {
            PHPickerConfiguration *configuration = [[PHPickerConfiguration alloc] init];
            configuration.filter = [PHPickerFilter imagesFilter];
            configuration.selectionLimit = 1;

            YuiPhotoPickerSharedDelegate = [YuiIOSPhotoPickerDelegate new];
            YuiPhotoPickerSharedDelegate.callbackObjectName = objectName;

            PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:configuration];
            picker.delegate = YuiPhotoPickerSharedDelegate;

            UIViewController *presenting = root.presentedViewController ?: root;
            [presenting presentViewController:picker animated:YES completion:nil];
            return;
        }

        YuiDocumentPickerSend(objectName, [YuiDocumentPickerErrorPrefix stringByAppendingString:@"このiOSバージョンでは写真選択に対応していません。"]);
    });
}

static void YuiDocumentPicker_OpenDocument(NSString *mode, NSString *objectName)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *root = UnityGetGLViewController();
        if (root == nil)
        {
            YuiDocumentPickerSend(objectName, [YuiDocumentPickerErrorPrefix stringByAppendingString:@"Unityの表示コントローラを取得できませんでした。"]);
            return;
        }

        NSArray<UTType *> *types = nil;
        if ([mode isEqualToString:@"vrm"])
        {
            UTType *vrmType = [UTType typeWithFilenameExtension:@"vrm"];
            types = vrmType != nil ? @[vrmType, UTTypeData] : @[UTTypeData];
        }
        else
        {
            types = @[UTTypeImage];
        }

        YuiDocumentPickerSharedDelegate = [YuiIOSDocumentPickerDelegate new];
        YuiDocumentPickerSharedDelegate.callbackObjectName = objectName;
        YuiDocumentPickerSharedDelegate.mode = mode;

        UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:types asCopy:YES];
        picker.delegate = YuiDocumentPickerSharedDelegate;
        picker.allowsMultipleSelection = NO;

        UIViewController *presenting = root.presentedViewController ?: root;
        [presenting presentViewController:picker animated:YES completion:nil];
    });
}

extern "C" void YuiIOSDocumentPicker_Open(const char *modeChars, const char *callbackObjectNameChars)
{
    NSString *mode = modeChars != NULL ? [NSString stringWithUTF8String:modeChars] : @"image";
    NSString *objectName = callbackObjectNameChars != NULL
        ? [NSString stringWithUTF8String:callbackObjectNameChars]
        : @"";

    if ([mode isEqualToString:@"image"])
    {
        YuiDocumentPicker_OpenPhoto(objectName);
        return;
    }

    YuiDocumentPicker_OpenDocument(mode, objectName);
}
