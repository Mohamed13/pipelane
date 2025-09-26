import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AsyncPipe, DatePipe, JsonPipe, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { PolicyService } from '../../core/policy.service';

@Component({
  standalone: true,
  selector: 'pl-conversation-thread',
  imports: [NgIf, NgFor, AsyncPipe, DatePipe, FormsModule, JsonPipe],
  template: `
  <h2>Conversation</h2>
  <div *ngIf="conversation | async as conv">
    <div *ngFor="let m of conv.messages" [style.textAlign]="m.direction===1?'right':'left'">
      <div style="display:inline-block; padding:.5rem; margin:.25rem 0; border-radius:.5rem; background:#f2f2f2">
        <small class="muted">{{ m.createdAt | date:'short' }}</small>
        <div>{{ m.payloadJson }}</div>
      </div>
    </div>
  </div>
  <div class="composer" *ngIf="canText; else templateBlock">
    <form (ngSubmit)="sendText()">
      <input placeholder="Type a message" [(ngModel)]="text" name="text" />
      <button type="submit">Send</button>
    </form>
  </div>
  <ng-template #templateBlock>
    <form (ngSubmit)="sendTemplate()">
      <input placeholder="Template name" [(ngModel)]="templateName" name="template" />
      <button type="submit">Send Template</button>
      <span class="muted">WhatsApp text disabled outside 24h window</span>
    </form>
  </ng-template>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConversationThreadComponent {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);
  private policy = inject(PolicyService);
  contactId = this.route.snapshot.paramMap.get('contactId')!;
  conversation = this.api.getConversation(this.contactId);
  canText = true;
  text = '';
  templateName = '';
  constructor(){
    this.conversation.subscribe((c:any)=>{
      const lastInbound = c.messages.filter((m:any)=>m.direction===0).slice(-1)[0]?.createdAt;
      this.canText = this.policy.isWhatsAppTextAllowed(lastInbound);
    });
  }
  sendText(){
    this.api.sendMessage({ contactId: this.contactId, channel:'whatsapp', type:'text', text: this.text }).subscribe();
    this.text='';
  }
  sendTemplate(){
    this.api.sendMessage({ contactId: this.contactId, channel:'whatsapp', type:'template', templateName: this.templateName }).subscribe();
    this.templateName='';
  }
}

