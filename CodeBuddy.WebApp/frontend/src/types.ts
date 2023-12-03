export enum EventType
{
    Connect,
    Message,
    Disconnect
}

export class CodeGram {
    constructor(public board: string, public userId: string, public type: EventType, public data: string) {
    }
}