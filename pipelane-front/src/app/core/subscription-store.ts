import type { Observable, PartialObserver } from 'rxjs';
import { Subscription } from 'rxjs';

/**
 * Utility to keep track of RxJS subscriptions and dispose of them in bulk.
 * Supports unnamed subscriptions via `track` and keyed subscriptions via `set`.
 */
export class SubscriptionStore {
  private readonly subscriptions = new Set<Subscription>();
  private readonly keyedSubscriptions = new Map<string, Subscription>();

  track(subscription: Subscription): Subscription {
    this.subscriptions.add(subscription);
    subscription.add(() => this.subscriptions.delete(subscription));
    return subscription;
  }

  set(key: string, subscription: Subscription): Subscription {
    const existing = this.keyedSubscriptions.get(key);
    if (existing) {
      existing.unsubscribe();
    }
    this.keyedSubscriptions.set(key, subscription);
    subscription.add(() => {
      if (this.keyedSubscriptions.get(key) === subscription) {
        this.keyedSubscriptions.delete(key);
      }
    });
    return this.track(subscription);
  }

  subscribe<T>(source: Observable<T>, observer: PartialObserver<T>, key?: string): Subscription {
    const subscription = source.subscribe(observer);
    if (key) {
      return this.set(key, subscription);
    }
    return this.track(subscription);
  }

  clear(): void {
    for (const subscription of this.keyedSubscriptions.values()) {
      subscription.unsubscribe();
    }
    this.keyedSubscriptions.clear();

    for (const subscription of this.subscriptions) {
      subscription.unsubscribe();
    }
    this.subscriptions.clear();
  }
}
