/* Finder entry: route to the retained bootstrap without loading replaceable .NET code. */
#include <CommonCrypto/CommonDigest.h>
#include <mach-o/dyld.h>
#include <sys/stat.h>
#include <pwd.h>
#include <unistd.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#ifndef APP_ID
#error APP_ID must bind this entry to its runtime
#endif
int main(int argc, char **argv) {
    char raw[PATH_MAX], exe[PATH_MAX], root[PATH_MAX], payload[PATH_MAX], host[PATH_MAX];
    uint32_t size = sizeof(raw);
    if (_NSGetExecutablePath(raw, &size) || !realpath(raw, exe)) return 1;
    char *leaf = strrchr(exe, '/'); if (!leaf) return 1; *leaf = 0;
    if (snprintf(payload, sizeof(payload), "%s", exe) >= sizeof(payload)) return 1;
    size_t length = strlen(exe), suffix = strlen("/Contents/MacOS");
    if (length <= suffix || strcmp(exe + length - suffix, "/Contents/MacOS")) return 1;
    memcpy(root, exe, length - suffix); root[length - suffix] = 0;
    unsigned char digest[CC_SHA256_DIGEST_LENGTH]; char hash[65];
    CC_SHA256(root, (CC_LONG)strlen(root), digest);
    for (int i=0; i<32; ++i) snprintf(hash + i*2, 3, "%02x", digest[i]);
    struct passwd *user = getpwuid(getuid()); if (!user) return 1;
    if (snprintf(host, sizeof(host), "%s/Library/Application Support/StarTournament/launcher/%s.recovery-%s/host/payload/Contents/MacOS/StarTournamentLauncher", user->pw_dir, APP_ID, hash) >= sizeof(host)) return 1;
    struct stat status;
    char parent[PATH_MAX]; strcpy(parent, host);
    for (char *cursor = parent + 1; *cursor; ++cursor) {
        if (*cursor != '/') continue;
        *cursor = 0;
        if (!lstat(parent, &status) && !S_ISDIR(status.st_mode)) return 1;
        *cursor = '/';
    }
    if (!lstat(host, &status)) {
        if (!S_ISREG(status.st_mode) || status.st_uid != getuid() || (status.st_mode & 0022)) return 1;
        char **next = calloc((size_t)argc + 3, sizeof(char*)); if (!next) return 1;
        next[0] = host; next[1] = "--bootstrap-payload"; next[2] = payload;
        for (int i=1; i<argc; ++i) next[i+2] = argv[i];
        execv(host, next); perror("Star Tournament bootstrap"); return 1;
    }
    if (errno != ENOENT) return 1;
    /* A damaged established bootstrap cannot silently revert to the replaceable launcher. */
    strcpy(parent, host); leaf = strrchr(parent, '/'); if (!leaf) return 1;
    strcpy(leaf + 1, "release.json");
    if (!lstat(parent, &status) || errno != ENOENT) return 1;
    /* Initial installation only. The packaged launcher atomically seeds the stable host. */
    if (snprintf(host, sizeof(host), "%s/StarTournamentLauncher", payload) >= sizeof(host)) return 1;
    argv[0] = host; execv(host, argv); perror("Star Tournament first launch"); return 1;
}
