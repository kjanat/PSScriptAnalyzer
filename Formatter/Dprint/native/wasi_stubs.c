#include <stdint.h>
#include <string.h>
#include <wasi/api.h>

#define pssa_sock_accept __wrap___wasi_sock_accept
#define pssa_args_get __wrap___wasi_args_get
#define pssa_args_sizes_get __wrap___wasi_args_sizes_get
#define pssa_environ_get __wrap___wasi_environ_get
#define pssa_environ_sizes_get __wrap___wasi_environ_sizes_get
#define pssa_clock_res_get __wrap___wasi_clock_res_get
#define pssa_clock_time_get __wrap___wasi_clock_time_get
#define pssa_fd_advise __wrap___wasi_fd_advise
#define pssa_fd_close __wrap___wasi_fd_close
#define pssa_fd_fdstat_get __wrap___wasi_fd_fdstat_get
#define pssa_fd_fdstat_set_flags __wrap___wasi_fd_fdstat_set_flags
#define pssa_fd_filestat_get __wrap___wasi_fd_filestat_get
#define pssa_fd_filestat_set_size __wrap___wasi_fd_filestat_set_size
#define pssa_fd_filestat_set_times __wrap___wasi_fd_filestat_set_times
#define pssa_fd_pread __wrap___wasi_fd_pread
#define pssa_fd_pwrite __wrap___wasi_fd_pwrite
#define pssa_fd_prestat_get __wrap___wasi_fd_prestat_get
#define pssa_fd_prestat_dir_name __wrap___wasi_fd_prestat_dir_name
#define pssa_fd_read __wrap___wasi_fd_read
#define pssa_fd_readdir __wrap___wasi_fd_readdir
#define pssa_fd_seek __wrap___wasi_fd_seek
#define pssa_fd_tell __wrap___wasi_fd_tell
#define pssa_fd_sync __wrap___wasi_fd_sync
#define pssa_fd_write __wrap___wasi_fd_write
#define pssa_path_create_directory __wrap___wasi_path_create_directory
#define pssa_path_filestat_get __wrap___wasi_path_filestat_get
#define pssa_path_filestat_set_times __wrap___wasi_path_filestat_set_times
#define pssa_path_link __wrap___wasi_path_link
#define pssa_path_open __wrap___wasi_path_open
#define pssa_path_readlink __wrap___wasi_path_readlink
#define pssa_path_remove_directory __wrap___wasi_path_remove_directory
#define pssa_path_rename __wrap___wasi_path_rename
#define pssa_path_unlink_file __wrap___wasi_path_unlink_file
#define pssa_poll_oneoff __wrap___wasi_poll_oneoff
#define pssa_proc_exit __wrap___wasi_proc_exit
#define pssa_random_get __wrap___wasi_random_get

extern __wasi_errno_t dprint_fd_write(
    __wasi_fd_t fd,
    const __wasi_ciovec_t *iovs,
    size_t iovs_len,
    __wasi_size_t *written
) __attribute__((import_module("env"), import_name("fd_write")));

__wasi_errno_t pssa_sock_accept(__wasi_fd_t fd, __wasi_fdflags_t flags, __wasi_fd_t *result) {
    (void)fd; (void)flags; (void)result;
    return __WASI_ERRNO_NOTSUP;
}

__wasi_errno_t __imported_wasi_snapshot_preview1_sock_accept(
    __wasi_fd_t fd,
    __wasi_fdflags_t flags,
    __wasi_fd_t *result
) {
    return pssa_sock_accept(fd, flags, result);
}

__wasi_errno_t sock_accept(__wasi_fd_t fd, __wasi_fdflags_t flags, __wasi_fd_t *result) {
    return pssa_sock_accept(fd, flags, result);
}

__wasi_errno_t pssa_args_get(uint8_t **argv, uint8_t *argv_buf) {
    (void)argv; (void)argv_buf;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_args_sizes_get(__wasi_size_t *argc, __wasi_size_t *argv_size) {
    *argc = 0;
    *argv_size = 0;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_environ_get(uint8_t **environ, uint8_t *environ_buf) {
    (void)environ; (void)environ_buf;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_environ_sizes_get(__wasi_size_t *count, __wasi_size_t *size) {
    *count = 0;
    *size = 0;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_clock_res_get(__wasi_clockid_t id, __wasi_timestamp_t *resolution) {
    (void)id;
    *resolution = 1000000;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_clock_time_get(__wasi_clockid_t id, __wasi_timestamp_t precision, __wasi_timestamp_t *time) {
    static __wasi_timestamp_t deterministic_time;
    (void)id; (void)precision;
    deterministic_time += 1000000;
    *time = deterministic_time;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_advise(__wasi_fd_t fd, __wasi_filesize_t offset, __wasi_filesize_t len, __wasi_advice_t advice) {
    (void)fd; (void)offset; (void)len; (void)advice;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_close(__wasi_fd_t fd) {
    (void)fd;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_fdstat_get(__wasi_fd_t fd, __wasi_fdstat_t *stat) {
    memset(stat, 0, sizeof(*stat));
    if (fd <= 2) {
        stat->fs_filetype = __WASI_FILETYPE_CHARACTER_DEVICE;
        return __WASI_ERRNO_SUCCESS;
    }
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_fdstat_set_flags(__wasi_fd_t fd, __wasi_fdflags_t flags) {
    (void)fd; (void)flags;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_filestat_get(__wasi_fd_t fd, __wasi_filestat_t *stat) {
    (void)fd;
    memset(stat, 0, sizeof(*stat));
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_filestat_set_size(__wasi_fd_t fd, __wasi_filesize_t size) {
    (void)fd; (void)size;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_filestat_set_times(
    __wasi_fd_t fd,
    __wasi_timestamp_t accessed,
    __wasi_timestamp_t modified,
    __wasi_fstflags_t flags
) {
    (void)fd; (void)accessed; (void)modified; (void)flags;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_pread(__wasi_fd_t fd, const __wasi_iovec_t *iovs, size_t iovs_len, __wasi_filesize_t offset, __wasi_size_t *read) {
    (void)fd; (void)iovs; (void)iovs_len; (void)offset;
    *read = 0;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_pwrite(__wasi_fd_t fd, const __wasi_ciovec_t *iovs, size_t iovs_len, __wasi_filesize_t offset, __wasi_size_t *written) {
    (void)fd; (void)iovs; (void)iovs_len; (void)offset;
    *written = 0;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_prestat_get(__wasi_fd_t fd, __wasi_prestat_t *prestat) {
    (void)fd; (void)prestat;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_prestat_dir_name(__wasi_fd_t fd, uint8_t *path, size_t path_len) {
    (void)fd; (void)path; (void)path_len;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_read(__wasi_fd_t fd, const __wasi_iovec_t *iovs, size_t iovs_len, __wasi_size_t *read) {
    (void)fd; (void)iovs; (void)iovs_len;
    *read = 0;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_readdir(__wasi_fd_t fd, uint8_t *buf, size_t buf_len, __wasi_dircookie_t cookie, __wasi_size_t *used) {
    (void)fd; (void)buf; (void)buf_len; (void)cookie;
    *used = 0;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_seek(__wasi_fd_t fd, __wasi_filedelta_t offset, __wasi_whence_t whence, __wasi_filesize_t *position) {
    (void)fd; (void)offset; (void)whence;
    *position = 0;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_tell(__wasi_fd_t fd, __wasi_filesize_t *position) {
    (void)fd;
    *position = 0;
    return __WASI_ERRNO_BADF;
}

__wasi_errno_t pssa_fd_sync(__wasi_fd_t fd) {
    (void)fd;
    return __WASI_ERRNO_SUCCESS;
}

__wasi_errno_t pssa_fd_write(__wasi_fd_t fd, const __wasi_ciovec_t *iovs, size_t iovs_len, __wasi_size_t *written) {
    return dprint_fd_write(fd, iovs, iovs_len, written);
}

__wasi_errno_t pssa_path_create_directory(__wasi_fd_t fd, const char *path) {
    (void)fd; (void)path;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_filestat_get(__wasi_fd_t fd, __wasi_lookupflags_t flags, const char *path, __wasi_filestat_t *stat) {
    (void)fd; (void)flags; (void)path; (void)stat;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_filestat_set_times(
    __wasi_fd_t fd,
    __wasi_lookupflags_t lookup_flags,
    const char *path,
    __wasi_timestamp_t accessed,
    __wasi_timestamp_t modified,
    __wasi_fstflags_t flags
) {
    (void)fd; (void)lookup_flags; (void)path; (void)accessed; (void)modified; (void)flags;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_link(
    __wasi_fd_t old_fd,
    __wasi_lookupflags_t old_flags,
    const char *old_path,
    __wasi_fd_t new_fd,
    const char *new_path
) {
    (void)old_fd; (void)old_flags; (void)old_path; (void)new_fd; (void)new_path;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_open(__wasi_fd_t fd, __wasi_lookupflags_t dirflags, const char *path, __wasi_oflags_t oflags, __wasi_rights_t rights_base, __wasi_rights_t rights_inheriting, __wasi_fdflags_t fdflags, __wasi_fd_t *opened_fd) {
    (void)fd; (void)dirflags; (void)path; (void)oflags;
    (void)rights_base; (void)rights_inheriting; (void)fdflags; (void)opened_fd;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_readlink(__wasi_fd_t fd, const char *path, uint8_t *buf, __wasi_size_t buf_len, __wasi_size_t *used) {
    (void)fd; (void)path; (void)buf; (void)buf_len;
    *used = 0;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_remove_directory(__wasi_fd_t fd, const char *path) {
    (void)fd; (void)path;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_rename(__wasi_fd_t fd, const char *old_path, __wasi_fd_t new_fd, const char *new_path) {
    (void)fd; (void)old_path; (void)new_fd; (void)new_path;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_path_unlink_file(__wasi_fd_t fd, const char *path) {
    (void)fd; (void)path;
    return __WASI_ERRNO_NOENT;
}

__wasi_errno_t pssa_poll_oneoff(const __wasi_subscription_t *subscriptions, __wasi_event_t *events, size_t count, __wasi_size_t *event_count) {
    (void)subscriptions; (void)events; (void)count;
    *event_count = 0;
    return __WASI_ERRNO_NOTSUP;
}

_Noreturn void pssa_proc_exit(__wasi_exitcode_t code) {
    (void)code;
    __builtin_trap();
}

__wasi_errno_t pssa_random_get(uint8_t *buf, __wasi_size_t len) {
    static uint32_t state = 0x9e3779b9u;
    for (__wasi_size_t i = 0; i < len; i++) {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        buf[i] = (uint8_t)state;
    }
    return __WASI_ERRNO_SUCCESS;
}
