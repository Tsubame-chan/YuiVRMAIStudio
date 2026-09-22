#import <Foundation/Foundation.h>
#import <PhotosUI/PhotosUI.h>
#import <ImageIO/ImageIO.h>
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

static NSString *YuiDocumentPickerTargetRoot(NSString *mode)
{
    return [[NSTemporaryDirectory() stringByAppendingPathComponent:@"YuiPickedFiles"]
        stringByAppendingPathComponent:[mode isEqualToString:@"image"] ? @"Image" : @"Avatar"];

}

static NSString *YuiDocumentPickerCopyURL(NSURL *url, NSString *mode, NSError **error)
{
    NSString *root = [YuiDocumentPickerTargetRoot(mode) stringByAppendingPathComponent:NSUUID.UUID.UUIDString];
    NSFileManager *fileManager = NSFileManager.defaultManager;
    if (![fileManager createDirectoryAtPath:root withIntermediateDirectories:YES attributes:nil error:error])
    {
        return nil;
    }

    NSString *filename = url.lastPathComponent;
    NSString *target = [root stringByAppendingPathComponent:filename];

    if ([fileManager fileExistsAtPath:target])
    {
        [fileManager removeItemAtPath:target error:nil];
    }

    if (![fileManager copyItemAtURL:url toURL:[NSURL fileURLWithPath:target] error:error])
    {
        [fileManager removeItemAtPath:target error:nil];
        return nil;
    }
    return target;
}

static NSString *YuiDocumentPickerCopyImageURL(NSURL *url, NSError **error)
{
    NSNumber *bytes = nil;
    [url getResourceValue:&bytes forKey:NSURLFileSizeKey error:error];
    if (bytes == nil || bytes.unsignedLongLongValue == 0 || bytes.unsignedLongLongValue > 64ULL * 1024 * 1024)
    {
        if (error != nil)
        {
            *error = [NSError errorWithDomain:@"YuiIOSPhotoPicker" code:1 userInfo:@{NSLocalizedDescriptionKey: bytes.unsignedLongLongValue == 0 ? @"The selected file is empty." : @"Choose an image smaller than 64 MB."}];
        }
        return nil;
    }

    NSString *root = YuiDocumentPickerTargetRoot(@"image");
    NSFileManager *fileManager = NSFileManager.defaultManager;
    if (![fileManager createDirectoryAtPath:root withIntermediateDirectories:YES attributes:nil error:error])
    {
        return nil;
    }

    CGImageSourceRef source = CGImageSourceCreateWithURL((__bridge CFURLRef)url, nil);
    if (!source) return nil;
    NSDictionary *options = @{(__bridge NSString *)kCGImageSourceCreateThumbnailFromImageAlways:@YES,
        (__bridge NSString *)kCGImageSourceCreateThumbnailWithTransform:@YES,
        (__bridge NSString *)kCGImageSourceThumbnailMaxPixelSize:@1600};
    CGImageRef thumbnail = CGImageSourceCreateThumbnailAtIndex(source, 0, (__bridge CFDictionaryRef)options);
    CFRelease(source);
    if (!thumbnail) return nil;
    NSMutableData *jpeg = [NSMutableData data];
    CGImageDestinationRef destination = CGImageDestinationCreateWithData((__bridge CFMutableDataRef)jpeg, CFSTR("public.jpeg"), 1, nil);
    if (!destination) { CGImageRelease(thumbnail); return nil; }
    CGImageDestinationAddImage(destination, thumbnail, (__bridge CFDictionaryRef)@{(__bridge NSString *)kCGImageDestinationLossyCompressionQuality:@0.9});
    BOOL encoded = CGImageDestinationFinalize(destination);
    CFRelease(destination); CGImageRelease(thumbnail);
    if (!encoded) return nil;

    NSString *safeExtension = @"jpg";
    NSString *filename = [NSString stringWithFormat:@"yui-picked-photo-%@.%@", NSUUID.UUID.UUIDString, safeExtension];
    NSString *target = [root stringByAppendingPathComponent:filename];
    return [jpeg writeToFile:target options:NSDataWritingAtomic error:error] ? target : nil;
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

    NSString *callback = self.callbackObjectName;
    NSString *mode = self.mode ?: @"image";
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        BOOL scoped = [url startAccessingSecurityScopedResource];
        NSFileCoordinator *coordinator = [[NSFileCoordinator alloc] initWithFilePresenter:nil];
        __block NSString *path = nil;
        __block NSError *copyError = nil;
        NSError *coordinationError = nil;
        [coordinator coordinateReadingItemAtURL:url options:0 error:&coordinationError byAccessor:^(NSURL *readingURL) {
            NSNumber *size = nil;
            [readingURL getResourceValue:&size forKey:NSURLFileSizeKey error:nil];
            if (size.unsignedLongLongValue > 512ULL * 1024 * 1024) {
                copyError = [NSError errorWithDomain:@"YuiFilePicker" code:1 userInfo:@{NSLocalizedDescriptionKey:@"Choose an avatar file smaller than 512 MB."}];
            } else path = YuiDocumentPickerCopyURL(readingURL, mode, &copyError);
        }];
        if (scoped) [url stopAccessingSecurityScopedResource];
        NSString *message = path.length > 0 ? path : [YuiDocumentPickerErrorPrefix stringByAppendingString:copyError.localizedDescription ?: coordinationError.localizedDescription ?: @"Could not read the selected file. Please select it again."];
        dispatch_async(dispatch_get_main_queue(), ^{
            YuiDocumentPickerSend(callback, message);
            YuiDocumentPickerSharedDelegate = nil;
        });
    });
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

    NSString *callbackObjectName = self.callbackObjectName;
    [provider loadFileRepresentationForTypeIdentifier:UTTypeImage.identifier completionHandler:^(NSURL * _Nullable url, NSError * _Nullable error) {
        NSString *message = nil;
        if (url != nil)
        {
            NSError *writeError = nil;
            NSString *path = YuiDocumentPickerCopyImageURL(url, &writeError);
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
        else if ([mode isEqualToString:@"avatar"])
        {
            UTType *zipType = [UTType typeWithFilenameExtension:@"zip"];
            UTType *vrmType = [UTType typeWithFilenameExtension:@"vrm"];
            NSMutableArray<UTType *> *avatarTypes = [NSMutableArray array];
            if (zipType != nil) [avatarTypes addObject:zipType];
            if (vrmType != nil) [avatarTypes addObject:vrmType];
            [avatarTypes addObject:UTTypeData];
            types = avatarTypes;
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
