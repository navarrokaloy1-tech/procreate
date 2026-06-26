import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BillingList } from './billing-list';

describe('BillingList', () => {
  let component: BillingList;
  let fixture: ComponentFixture<BillingList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [BillingList],
    }).compileComponents();

    fixture = TestBed.createComponent(BillingList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
