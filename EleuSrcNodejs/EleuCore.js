"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.CreateBoolVal = exports.BoolFalse = exports.BoolTrue = exports.CreateStringVal = exports.CreateNumberVal = exports.Nil = exports.Value = exports.ValueType = void 0;
var ValueType;
(function (ValueType) {
    ValueType[ValueType["VAL_NIL"] = 0] = "VAL_NIL";
    ValueType[ValueType["VAL_BOOL"] = 1] = "VAL_BOOL";
    ValueType[ValueType["VAL_NUMBER"] = 2] = "VAL_NUMBER";
    ValueType[ValueType["VAL_STRING"] = 3] = "VAL_STRING";
    ValueType[ValueType["VAL_LIST"] = 4] = "VAL_LIST";
    ValueType[ValueType["VAL_OBJ"] = 5] = "VAL_OBJ";
})(ValueType = exports.ValueType || (exports.ValueType = {}));
class Value {
    constructor(type, value) {
        this.type = type;
        this.value = value;
    }
    toString() {
        return this.value.toString();
    }
}
exports.Value = Value;
exports.Nil = new Value(ValueType.VAL_NIL, null);
const CreateNumberVal = (value) => new Value(ValueType.VAL_NUMBER, value);
exports.CreateNumberVal = CreateNumberVal;
const CreateStringVal = (value) => new Value(ValueType.VAL_STRING, value);
exports.CreateStringVal = CreateStringVal;
exports.BoolTrue = new Value(ValueType.VAL_BOOL, 1);
exports.BoolFalse = new Value(ValueType.VAL_BOOL, 0);
const CreateBoolVal = (value) => value ? exports.BoolTrue : exports.BoolFalse;
exports.CreateBoolVal = CreateBoolVal;
//# sourceMappingURL=EleuCore.js.map