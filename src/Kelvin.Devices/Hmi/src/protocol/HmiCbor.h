#pragma once

#include <cbor.h>
#include <math.h>
#include <HmiProtocol.h>

namespace HmiCbor
{
  inline bool readFloat(const CborValue *value, float &result)
  {
    if (!value)
      return false;
    if (cbor_value_is_half_float(value))
    {
      uint16_t bits;
      if (cbor_value_get_half_float(value, &bits) != CborNoError)
        return false;
      int exponent = (bits >> 10) & 31;
      int fraction = bits & 1023;
      if (exponent == 31)
        return false;
      result = exponent == 0 ? ldexpf((float)fraction, -24) : ldexpf((float)(1024 + fraction), exponent - 25);
      if (bits & 0x8000)
        result = -result;
    }
    else if (cbor_value_is_float(value))
    {
      if (cbor_value_get_float(value, &result) != CborNoError)
        return false;
    }
    else if (cbor_value_is_double(value))
    {
      double number;
      if (cbor_value_get_double(value, &number) != CborNoError)
        return false;
      result = (float)number;
    }
    else
      return false;
    return isfinite(result);
  }

  inline bool skipValue(CborValue &value, size_t depth = 0)
  {
    if (cbor_value_is_tag(&value) || !cbor_value_is_length_known(&value))
      return false;
    if (!cbor_value_is_container(&value))
      return cbor_value_advance(&value) == CborNoError;
    if (depth >= 4)
      return false;
    CborValue child;
    if (cbor_value_enter_container(&value, &child) != CborNoError)
      return false;
    while (!cbor_value_at_end(&child))
      if (!skipValue(child, depth + 1))
        return false;
    return cbor_value_leave_container(&value, &child) == CborNoError;
  }

  class Map
  {
    CborParser parser;
    uint32_t keys[32];
    CborValue values[32];
    size_t count = 0;

  public:
    bool parse(const uint8_t *body, size_t length)
    {
      CborValue root;
      return body && length > 0 && length <= 2048 &&
             cbor_parser_init(body, length, 0, &parser, &root) == CborNoError && read(root, true) &&
             cbor_value_get_next_byte(&root) == body + length;
    }

    bool read(CborValue &root, bool versioned = false)
    {
      if (!cbor_value_is_map(&root) || cbor_value_get_map_length(&root, &count) != CborNoError || count > 32)
        return false;
      CborValue fields;
      if (cbor_value_enter_container(&root, &fields) != CborNoError)
        return false;
      for (size_t index = 0; index < count; index++)
      {
        uint64_t key;
        if (!cbor_value_is_unsigned_integer(&fields) || cbor_value_get_uint64(&fields, &key) != CborNoError || key > UINT32_MAX)
          return false;
        for (size_t previous = 0; previous < index; previous++)
          if (keys[previous] == key)
            return false;
        keys[index] = (uint32_t)key;
        if (cbor_value_advance_fixed(&fields) != CborNoError || cbor_value_at_end(&fields))
          return false;
        values[index] = fields;
        if (!skipValue(fields))
          return false;
      }
      uint32_t version;
      return cbor_value_at_end(&fields) && cbor_value_leave_container(&root, &fields) == CborNoError &&
             (!versioned || (unsignedValue(0, version, 1) && version == 1));
    }

    const CborValue *find(uint32_t key) const
    {
      for (size_t index = 0; index < count; index++)
        if (keys[index] == key)
          return &values[index];
      return nullptr;
    }

    bool unsignedValue(uint32_t key, uint32_t &result, uint32_t maximum = UINT32_MAX) const
    {
      auto value = find(key);
      uint64_t number;
      if (!value || !cbor_value_is_unsigned_integer(value) || cbor_value_get_uint64(value, &number) != CborNoError || number > maximum)
        return false;
      result = (uint32_t)number;
      return true;
    }

    bool boolean(uint32_t key, bool &result) const
    {
      auto value = find(key);
      return value && cbor_value_is_boolean(value) && cbor_value_get_boolean(value, &result) == CborNoError;
    }

    bool number(uint32_t key, float &result) const { return readFloat(find(key), result); }

    bool nullableNumber(uint32_t key, bool &hasValue, float &result) const
    {
      auto value = find(key);
      if (!value)
        return false;
      hasValue = !cbor_value_is_null(value);
      return !hasValue || readFloat(value, result);
    }

    bool id(uint32_t key, uint8_t *result) const
    {
      auto value = find(key);
      size_t length = 16;
      return value && cbor_value_is_byte_string(value) &&
             cbor_value_copy_byte_string(value, result, &length, nullptr) == CborNoError && length == 16;
    }

    bool array(uint32_t key, CborValue &elements, size_t &length, size_t maximum) const
    {
      auto value = find(key);
      return value && cbor_value_is_array(value) && cbor_value_get_array_length(value, &length) == CborNoError &&
             length <= maximum && cbor_value_enter_container(value, &elements) == CborNoError;
    }
  };

  class Writer
  {
    CborEncoder encoder;
    CborEncoder map;
    uint8_t *buffer;
    HmiMessageType type;
    bool valid;

    bool key(uint32_t field) { return valid = valid && cbor_encode_uint(&map, field) == CborNoError; }

  public:
    Writer(uint8_t *output, size_t size, HmiMessageType messageType, size_t fieldCount) : buffer(output), type(messageType)
    {
      valid = size >= HmiEnvelope::HEADER_SIZE;
      if (!valid)
        return;
      cbor_encoder_init(&encoder, output + HmiEnvelope::HEADER_SIZE, size - HmiEnvelope::HEADER_SIZE, 0);
      valid = cbor_encoder_create_map(&encoder, &map, fieldCount + 1) == CborNoError;
      unsignedValue(0, 1);
    }

    void unsignedValue(uint32_t field, uint32_t value) { valid = key(field) && cbor_encode_uint(&map, value) == CborNoError; }
    void boolean(uint32_t field, bool value) { valid = key(field) && cbor_encode_boolean(&map, value) == CborNoError; }
    void number(uint32_t field, float value) { valid = key(field) && isfinite(value) && cbor_encode_float(&map, value) == CborNoError; }
    void nullableNumber(uint32_t field, const float *value)
    {
      if (value)
        number(field, *value);
      else
        valid = key(field) && cbor_encode_null(&map) == CborNoError;
    }
    void id(uint32_t field, const uint8_t *value)
    {
      valid = key(field) && (value ? cbor_encode_byte_string(&map, value, 16) : cbor_encode_null(&map)) == CborNoError;
    }

    size_t finish()
    {
      if (!valid || cbor_encoder_close_container_checked(&encoder, &map) != CborNoError)
        return 0;
      size_t length = cbor_encoder_get_buffer_size(&encoder, buffer + HmiEnvelope::HEADER_SIZE);
      static uint32_t messageId = 0;
      HmiEnvelope::writeHeader(buffer, type, 0, 1, (uint16_t)length, ++messageId);
      return HmiEnvelope::HEADER_SIZE + length;
    }
  };
}