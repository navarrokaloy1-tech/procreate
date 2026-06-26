import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VisitList } from './visit-list';

describe('VisitList', () => {
  let component: VisitList;
  let fixture: ComponentFixture<VisitList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [VisitList],
    }).compileComponents();

    fixture = TestBed.createComponent(VisitList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
