#include <cbor.h>
#include <DeviceEnvelope.h>
#include <HumidifierProtocol.h>
#include "HumidifierMessages.h"

namespace HumidifierMessages
{
  size_t encodeRelayState(uint8_t *buffer, size_t size, HumidifierMessageType type, bool relayOn)
  {
    if (size <= DeviceEnvelope::HEADER_SIZE)
      return 0;

    uint8_t *body = buffer + DeviceEnvelope::HEADER_SIZE;
    CborEncoder encoder;
    CborEncoder map;
    cbor_encoder_init(&encoder, body, size - DeviceEnvelope::HEADER_SIZE, 0);
    if (cbor_encoder_create_map(&encoder, &map, 2) != CborNoError ||
        cbor_encode_uint(&map, 0) != CborNoError || cbor_encode_uint(&map, 1) != CborNoError ||
        cbor_encode_uint(&map, 1) != CborNoError || cbor_encode_boolean(&map, relayOn) != CborNoError ||
        cbor_encoder_close_container(&encoder, &map) != CborNoError)
      return 0;

    static uint32_t messageId = 0;
    size_t bodyLength = cbor_encoder_get_buffer_size(&encoder, body);
    DeviceEnvelope::writeHeader(buffer, (uint8_t)type, 0, 1, (uint16_t)bodyLength, ++messageId);
    return DeviceEnvelope::HEADER_SIZE + bodyLength;
  }

  bool decodeState(const uint8_t *frame, size_t length, bool &enabled)
  {
    uint8_t type;
    uint8_t chunkIndex;
    uint8_t chunkCount;
    uint16_t bodyLength;
    uint32_t messageId;
    if (!DeviceEnvelope::readHeader(frame, length, type, chunkIndex, chunkCount, bodyLength, messageId) ||
        type != (uint8_t)HumidifierMessageType::State || chunkCount != 1 || bodyLength == 0)
      return false;

    CborParser parser;
    CborValue root;
    CborValue field;
    if (cbor_parser_init(frame + DeviceEnvelope::HEADER_SIZE, bodyLength, 0, &parser, &root) != CborNoError || !cbor_value_is_map(&root) ||
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
