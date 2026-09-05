/* moonlight — a Monero wallet, as a linkable library.
 *
 * The library does no networking. It tells you what to send and where; you send it
 * however your application already sends things, and hand back the answer. That way
 * the wallet uses your trust store, your proxy settings and your idea of a timeout,
 * and there is no thread of ours calling back into yours.
 *
 * Nothing here allocates memory you have to free. Where a result is bytes or text,
 * you offer a buffer and are told the size needed if it was too small.
 *
 * No function throws. Every one returns a moonlight_status.
 *
 * A handle is not thread-safe: one thread at a time per wallet.
 */

#ifndef MOONLIGHT_H
#define MOONLIGHT_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
    MOONLIGHT_OK = 0,
    MOONLIGHT_DONE = 1,               /* nothing more to do; not a failure   */
    MOONLIGHT_BAD_ARGUMENT = -1,
    MOONLIGHT_BUFFER_TOO_SMALL = -2,  /* *needed says how much is wanted     */
    MOONLIGHT_CANNOT_OPEN = -3,       /* not a wallet, or the wrong password */
    MOONLIGHT_BAD_RESPONSE = -4,
    MOONLIGHT_WRONG_STATE = -5,
    MOONLIGHT_FAILED = -100
} moonlight_status;

typedef intptr_t moonlight_wallet;

/* The interface version. Refuse a library whose answer you do not know. */
int32_t moonlight_abi_version(void);

/* Both strings are UTF-8 and zero-terminated. Close what you open. */
moonlight_status moonlight_wallet_open(const uint8_t *path,
                                       const uint8_t *password,
                                       moonlight_wallet *handle);

moonlight_status moonlight_wallet_close(moonlight_wallet handle);

/* UTF-8, zero-terminated. *needed counts the zero. */
moonlight_status moonlight_wallet_address(moonlight_wallet handle,
                                          uint8_t *buffer,
                                          int32_t capacity,
                                          int32_t *needed);

/* Atomic units: 1 XMR is 1000000000000. */
moonlight_status moonlight_wallet_balance(moonlight_wallet handle,
                                          uint64_t *total,
                                          uint64_t *unlocked);

/* The next block to read, so the last one read is one below it. */
moonlight_status moonlight_wallet_scanned_height(moonlight_wallet handle,
                                                 uint64_t *height);

moonlight_status moonlight_wallet_save(moonlight_wallet handle);

/* Following the chain.
 *
 *   for (;;) {
 *       s = moonlight_sync_next(w, path, sizeof path, &pn, body, sizeof body, &bn);
 *       if (s == MOONLIGHT_DONE) break;
 *       if (s != MOONLIGHT_OK) handle it;
 *
 *       POST body to <daemon>/<path>;
 *
 *       if (sent)  moonlight_sync_supply(w, answer, answer_length);
 *       else if (moonlight_sync_failed(w) != MOONLIGHT_OK) give up;
 *   }
 *
 * Both sizes are reported even when only one buffer was too small, so growing them
 * costs one extra call rather than two. Every request is a POST.
 */
moonlight_status moonlight_sync_next(moonlight_wallet handle,
                                     uint8_t *path, int32_t path_capacity, int32_t *path_needed,
                                     uint8_t *body, int32_t body_capacity, int32_t *body_needed);

moonlight_status moonlight_sync_supply(moonlight_wallet handle,
                                       const uint8_t *response,
                                       int32_t length);

/* The request could not be sent. MOONLIGHT_OK means the engine carried on without
 * it; MOONLIGHT_FAILED means it could not, and the failure is yours to deal with. */
moonlight_status moonlight_sync_failed(moonlight_wallet handle);

/* Begin another sweep. A wallet left open does this every ten seconds or so. */
moonlight_status moonlight_sync_restart(moonlight_wallet handle);

moonlight_status moonlight_sync_progress(moonlight_wallet handle,
                                         uint64_t *scanned,
                                         uint64_t *chain_height,
                                         int32_t *outputs);

#ifdef __cplusplus
}
#endif

#endif /* MOONLIGHT_H */
