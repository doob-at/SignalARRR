"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ClientRequestMessage = void 0;
class ClientRequestMessage {
    constructor(methodName, ...args) {
        this.Method = methodName;
        this.Arguments = args;
    }
    WithAuthorization(authorization) {
        if (authorization instanceof Function) {
            authorization = authorization();
        }
        this.Authorization = authorization;
        return this;
    }
}
exports.ClientRequestMessage = ClientRequestMessage;
