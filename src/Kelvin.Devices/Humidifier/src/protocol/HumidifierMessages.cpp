#include <cbor.h>
#include <HumidifierProtocol.h>
#include "HumidifierMessages.h"

namespace HumidifierMessages
{
  size_t encodePing(uint8_t *buffer, size_t size, bool relayOn)
  {
    if (size < 2)
      return 0;

    buffer[0] = (uint8_t)HumidifierMessageType::Ping;

    CborEncoder encoder;
    CborEncoder map;
    cbor_encoder_init(&encoder, buffer + 1, size - 1, 0);
    if (cbor_encoder_create_map(&encoder, &map, 2) != CborNoError ||
        cbor_encode_uint(&map, 0) != CborNoError || cbor_encode_uint(&map, 1) != CborNoError ||
        cbor_encode_uint(&map, 1) != CborNoError || cbor_encode_boolean(&map, relayOn) != CborNoError ||
        cbor_encoder_close_container(&encoder, &map) != CborNoError)
      return 0;

    return 1 + cbor_encoder_get_buffer_size(&encoder, buffer + 1);
  }

  bool decodeState(const uint8_t *frame, size_t length, bool &enabled)
  {
    if (length < 2 || frame[0] != (uint8_t)HumidifierMessageType::State)
      return false;

    CborParser parser;
    CborValue root;
    CborValue field;
    if (cbor_parser_init(frame + 1, length - 1, 0, &parser, &root) != CborNoError || !cbor_value_is_map(&root) ||
        cbor_value_enter_container(&root, &field) != CborNoError)
      return false;

    bool hasVersion = false;
    bool hasEnabled = false;
    while (!cbor_value_at_end(&field))
    {
      uint64_t key;
      if (!cbor_value_is_unsigned_integer(&field) || cbor_value_get_uint64(&field, &key) != CborNoError ||
          cbor_value_advance_fixed(&field) != CborNoError || cbor_value_at_end(&field))
        return false;

      if (key == 0)
      {
        uint64_t version;
        if (!cbor_value_is_unsigned_integer(&field) || cbor_value_get_uint64(&field, &version) != CborNoError || version != 1)
          return false;
        hasVersion = true;
      }
      else if (key == 1)
      {
        if (!cbor_value_is_boolean(&field) || cbor_value_get_boolean(&field, &enabled) != CborNoError)
          return false;
        hasEnabled = true;
      }

      if (cbor_value_advance(&field) != CborNoError)
        return false;
    }

    return hasVersion && hasEnabled;
  }
}
