/**
 * What a failed call throws.
 *
 * The answer carries ONE status - the transport can only carry one - and AS MANY errors as there
 * are things wrong, because a form with three bad fields is the ordinary case, not the exception.
 * The status arrives as the HTTP status code; the body is the list.
 */

/** One thing that went wrong, in the caller's own terms. */
export interface ApiFieldError {
    /**
     * Which field it is about: "items[1].quantity", "billingAddress.country". Empty when the error
     * is not about a field ("this order does not exist"). This is what lets a form mark the right
     * control instead of only showing a sentence.
     */
    path: string;
    messageText: string;
    additionalInformation?: string | null;
}

export class ApiError extends Error {
    /** The HTTP status of the answer. */
    readonly status: number;
    /** The operation that failed, for logs and for the message. */
    readonly operation: string;
    /** Every error the server reported - never just the first. */
    readonly errors: ApiFieldError[];

    constructor(status: number, operation: string, errors: ApiFieldError[]) {
        super(ApiError.buildMessage(operation, errors));
        this.name = "ApiError";
        this.status = status;
        this.operation = operation;
        this.errors = errors;
        // extending a built-in needs the prototype restored when the target is ES5
        Object.setPrototypeOf(this, ApiError.prototype);
    }

    /** The error on a given field, if the server reported one. */
    errorFor(path: string): ApiFieldError | undefined {
        return this.errors.find((error) => error.path === path);
    }

    /** Field errors keyed by path, ready to hand to a form. */
    byPath(): Record<string, string> {
        const byPath: Record<string, string> = {};
        for (const error of this.errors) {
            if (error.path) {
                byPath[error.path] = error.messageText;
            }
        }
        return byPath;
    }

    /** True when the server named at least one field - i.e. the caller can fix this. */
    hasFieldErrors(): boolean {
        return this.errors.some((error) => !!error.path);
    }

    private static buildMessage(operation: string, errors: ApiFieldError[]): string {
        if (errors.length === 0) {
            return `${operation} failed`;
        }
        return `${operation} failed: ${errors
            .map((error) => (error.path ? `${error.path}: ${error.messageText}` : error.messageText))
            .join("; ")}`;
    }
}
