export enum ValueType
{
  VAL_NIL, // must go first to ensure, new Values are nil
  VAL_BOOL,
  VAL_NUMBER,
  VAL_STRING,
  VAL_LIST,  // List<Value>
  VAL_OBJ
}

export class Value
{
  type: ValueType;
  value: any;

  constructor(type: ValueType, value: any)
  {
    this.type = type;
    this.value = value;
  }

  toString(): string
  {
    return this.value.toString();
  }
}

export const Nil = new Value(ValueType.VAL_NIL, null);
export const CreateNumberVal = (value: number): Value => new Value(ValueType.VAL_NUMBER, value);
export const CreateStringVal = (value: string): Value => new Value(ValueType.VAL_STRING, value);
export const BoolTrue: Value = new Value(ValueType.VAL_BOOL, 1);
export const BoolFalse: Value = new Value(ValueType.VAL_BOOL, 0);
export const CreateBoolVal= (value: boolean): Value => value ? BoolTrue : BoolFalse;


