#import <Foundation/Foundation.h>
#include <mach/mach.h>

extern "C" unsigned long long YuiMemoryDiagnostics_GetResidentBytes()
{
    task_vm_info_data_t info;
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    const kern_return_t result = task_info(
        mach_task_self(),
        TASK_VM_INFO,
        reinterpret_cast<task_info_t>(&info),
        &count);
    if (result != KERN_SUCCESS) {
        return 0;
    }

    return static_cast<unsigned long long>(info.phys_footprint);
}
