#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <TargetConditionals.h>
#include <mutex>
#include <stdlib.h>
#include <string.h>

// File-based macOS Keychain may ask for access again after a development
// signature changes. Never let that dialog block Unity's main thread at startup.
// Explicit recovery is called on a worker; overlapping calls fail immediately.
static std::mutex credentialMutex;
class CredentialInteraction {
    std::unique_lock<std::mutex> lock;
#if TARGET_OS_OSX
    Boolean previous = true;
#endif
public:
    explicit CredentialInteraction(bool interactive) : lock(credentialMutex, std::try_to_lock) {
#if TARGET_OS_OSX
        if (lock.owns_lock()) {
            SecKeychainGetUserInteractionAllowed(&previous);
            SecKeychainSetUserInteractionAllowed(interactive);
        }
#endif
    }
    ~CredentialInteraction() {
#if TARGET_OS_OSX
        if (lock.owns_lock()) SecKeychainSetUserInteractionAllowed(previous);
#endif
    }
    bool acquired() const { return lock.owns_lock(); }
};

// The application identifier separates personal, public and validation credentials.
static NSMutableDictionary *YuiCredentialQuery(const char *service) {
    return [@{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: [NSString stringWithUTF8String:service],
        (__bridge id)kSecAttrAccount: @"openai-api-key"} mutableCopy];
}
static int ReadCredential(const char *service, void **result, bool interactive) {
    *result = NULL;
    CredentialInteraction interaction(interactive);
    if (!interaction.acquired()) return errSecInteractionNotAllowed;
    NSMutableDictionary *query = YuiCredentialQuery(service);
#if !TARGET_OS_OSX
    if (!interactive) query[(__bridge id)kSecUseAuthenticationUI] = (__bridge id)kSecUseAuthenticationUIFail;
#endif
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    CFTypeRef value = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &value);
    if (status == errSecItemNotFound) return 0;
    if (status != errSecSuccess) return (int)status;
    NSData *data = CFBridgingRelease(value);
    char *bytes = (char *)calloc(data.length + 1, 1);
    if (!bytes) return -1;
    memcpy(bytes, data.bytes, data.length);
    *result = bytes;
    return 0;
}
extern "C" int YuiCredentialRead(const char *service, void **result) {
    return ReadCredential(service, result, false);
}
extern "C" int YuiCredentialReadInteractive(const char *service, void **result) {
    return ReadCredential(service, result, true);
}
extern "C" int YuiCredentialWrite(const char *service, const char *text) {
    CredentialInteraction interaction(false);
    if (!interaction.acquired()) return errSecInteractionNotAllowed;
    NSMutableDictionary *query = YuiCredentialQuery(service);
    if (!text || !*text) {
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)query);
        return status == errSecItemNotFound ? 0 : (int)status;
    }
    NSData *data = [[NSString stringWithUTF8String:text] dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *attributes = @{(__bridge id)kSecValueData:data,
        (__bridge id)kSecAttrAccessible:(__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly};
    OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)attributes);
    if (status == errSecItemNotFound) {
        [query addEntriesFromDictionary:attributes];
        status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
    }
    return (int)status;
}
extern "C" void YuiCredentialFree(void *value) {
    if (!value) return;
    volatile char *p = (volatile char *)value;
    size_t length = strlen((const char *)value);
    while (length--) *p++ = 0;
    free(value);
}
