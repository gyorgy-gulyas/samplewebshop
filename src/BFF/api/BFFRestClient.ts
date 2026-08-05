import axios from "axios";
import type { AxiosError, AxiosInstance, AxiosHeaderValue } from "axios";
import { ApiError } from "./ApiError";
import type { ApiFieldError } from "./ApiError";

/**
 * The one hand-written piece of the BFF client: the generated per-interface clients call into it
 * for the axios instance, the per-call headers and the error translation. The emitter never writes
 * this file.
 *
 * The header names are the ones ServiceKit's CallingContext actually reads on the other side
 * (ServiceConstans) - anything else is silently dropped, so they are spelled out here once.
 */
export class BFFRestClient {
    private static instance: BFFRestClient;

    public axios: AxiosInstance;

    private constructor() {
        this.axios = axios.create({
            baseURL: "/",
            timeout: 30000,
            headers: {
                "Content-Type": "application/json",
            },
        });
    }

    public static getInstance(): BFFRestClient {
        if (!BFFRestClient.instance) {
            BFFRestClient.instance = new BFFRestClient();
        }
        return BFFRestClient.instance;
    }

    /** Points the client at a service and describes who is calling. */
    public init(baseURL: string, clientLanguage: string, appName: string, appVersion: string): void {
        this.axios.defaults.baseURL = baseURL;

        const common = this.axios.defaults.headers.common;
        common["client-language"] = clientLanguage;
        common["client-application"] = appName;
        common["client-version"] = appVersion;
        common["client-tz-offset"] = new Date().getTimezoneOffset();
    }

    /** The bearer token plus the identity the server records against every call. */
    public setAuthorization(bearerToken: string, identityId: string, identityName: string): void {
        const common = this.axios.defaults.headers.common;
        common["Authorization"] = `Bearer ${bearerToken}`;
        common["identity-id"] = identityId;
        common["identity-name"] = identityName;
    }

    public clearAuthorization(): void {
        const common = this.axios.defaults.headers.common;
        delete common["Authorization"];
        delete common["identity-id"];
        delete common["identity-name"];
    }

    /** Ties every call in one user action together in the logs. */
    public setTenant(tenantId: string): void {
        this.axios.defaults.headers.common["tenant-id"] = tenantId;
    }

    /**
     * The per-call headers. A fresh correlation id each time, and the operation as the call stack
     * root - the server appends to it as the call travels on, which is what makes one request
     * followable across services.
     */
    public getRequestHeaders(operation: string): Record<string, AxiosHeaderValue> {
        return {
            "correlation-id": BFFRestClient.newCorrelationId(),
            "call-stack": operation,
        };
    }

    /**
     * Turns a failed call into an ApiError.
     *
     * The body of a failure is a LIST - the server names every field that is wrong at once, so the
     * caller does not have to fix them one round trip at a time - and the status is the HTTP status
     * code, because that is the one thing the transport can carry. Older or foreign endpoints may
     * still answer with a single error object or with plain text, so both are accepted rather than
     * lost.
     */
    public mapApiError(error: AxiosError, operation: string): ApiError {
        const response = error.response;

        if (response) {
            return new ApiError(response.status, operation, BFFRestClient.readErrors(response.data, error));
        }

        if (error.request) {
            // the request went out and nothing came back: a timeout, a dropped connection, CORS
            return new ApiError(0, operation, [{ path: "", messageText: `No response received: ${error.message}` }]);
        }

        return new ApiError(0, operation, [{ path: "", messageText: error.message }]);
    }

    private static readErrors(data: unknown, error: AxiosError): ApiFieldError[] {
        if (Array.isArray(data)) {
            return data.map((item) => BFFRestClient.readError(item));
        }
        if (data && typeof data === "object") {
            return [BFFRestClient.readError(data)];
        }
        if (typeof data === "string" && data.length > 0) {
            return [{ path: "", messageText: data }];
        }
        return [{ path: "", messageText: error.message }];
    }

    private static readError(item: unknown): ApiFieldError {
        const source = (item ?? {}) as Record<string, unknown>;
        return {
            path: typeof source.path === "string" ? source.path : "",
            messageText: typeof source.messageText === "string" ? source.messageText : JSON.stringify(item),
            additionalInformation:
                typeof source.additionalInformation === "string" ? source.additionalInformation : null,
        };
    }

    private static newCorrelationId(): string {
        // crypto.randomUUID is the right answer everywhere it exists; the fallback keeps the sample
        // usable over plain http and on older runtimes, where it is absent
        const cryptoApi = globalThis.crypto as Crypto | undefined;
        if (cryptoApi?.randomUUID) {
            return cryptoApi.randomUUID();
        }
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (character) => {
            const random = (Math.random() * 16) | 0;
            const value = character === "x" ? random : (random & 0x3) | 0x8;
            return value.toString(16);
        });
    }
}
