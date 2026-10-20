import { test } from "node:test";
import assert from "node:assert/strict";
import { fixedComboOptions, fixedComboSummary } from "../src/features/journey/combo/fixedComboModel.ts";
import { comboDetailModel, chooseDetailTemplate, detailBasePrice } from "../src/features/journey/combo/comboDetailModel.ts";
import { EMPTY_CART, selectService, removeService } from "../src/features/journey/combo/cartModel.ts";
import { editableComboCart } from "../src/features/journey/combo/comboEditing.ts";
import { comboCardModel } from "../src/features/journey/customerOrders/comboOrderModel.ts";

const component = (id, price) => ({ serviceItemId:id,listingId:`listing-${id}`,vendorId:"shop",categoryId:"photo",name:`Frozen ${id}`,listingTitle:`Listing ${id}`,basePrice:price,unit:"Package",quantity:1,minGuests:1,maxGuests:500,regions:"Hà Nội",cancellationPolicy:"Frozen policy",description:"Published description",imageUrl:`https://example.invalid/frozen-${id}.jpg`,galleryUrls:[],variants:[],addOns:[],sourceVersion:"version-1" });
function catalog() {
  const components=[component("one",5000000),component("two",5000000)];
  return {categories:[{categoryId:"photo",name:"Photo",options:components.map(c=>({serviceItemId:c.serviceItemId,listingId:c.listingId,vendorId:"shop",categoryId:"photo",price:999999999,title:"Changed live name",unit:"Guest",variants:[],addOns:[],galleryUrls:[]}))}],
    templates:[{id:"fixed",name:"Fixed package",ownerVendorId:"shop",ownerVendorName:"Frozen shop",pricingMode:"FixedPrice",comboPrice:8500000,originalPrice:10000000,components,items:components.map(c=>({categoryId:c.categoryId,listingId:c.listingId,serviceItemId:c.serviceItemId,isOptional:false}))}],discountTiers:[]};
}
test("fixed detail uses frozen service names, descriptions and prices instead of live catalog",()=>{
  const model=comboDetailModel(catalog(),"fixed"); assert.equal(model.canChoose,true);
  assert.equal(model.required[0].item.title,"Frozen one"); assert.equal(model.required[0].item.price,5000000);
  assert.equal(model.required[0].item.description,"Published description"); assert.match(model.photos.join(" "),/frozen-one/);
  assert.equal(detailBasePrice(model,200),10000000); assert.equal(detailBasePrice(model,500),10000000);
});
test("multiple independent fixed services may share a category, but never a service ID",()=>{
  const data=catalog(); assert.equal(comboDetailModel(data,"fixed").required.length,2);
  data.templates[0].items[1].serviceItemId="one"; assert.equal(comboDetailModel(data,"fixed").canChoose,false);
});
test("fixed package cannot be selected with missing snapshots, one item or review-required dependency",()=>{
  for(const mutate of [t=>{t.components=[];},t=>{t.items=t.items.slice(0,1);t.components=t.components.slice(0,1);},t=>{t.requiresComponentReview=true;},t=>{t.components[0].vendorId="foreign";}]){
    const data=catalog();mutate(data.templates[0]);assert.equal(comboDetailModel(data,"fixed").canChoose,false);
  }
});
test("fixed savings follow 10m retail and 8.5m sale without making negative saving claims",()=>{
  const template=catalog().templates[0]; assert.deepEqual(fixedComboSummary(template),{price:8500000,original:10000000,saving:1500000,percentage:15});
  assert.equal(fixedComboSummary({...template,comboPrice:12000000}).saving,0);
  for(const value of [null,NaN,Infinity,0,-1]) assert.equal(fixedComboSummary({...template,comboPrice:value}),null);
});
test("choosing fixed package retains event info and both same-category services, locks modification",()=>{
  const model=comboDetailModel(catalog(),"fixed");
  const cart=chooseDetailTemplate({...EMPTY_CART,region:"Hà Nội",weddingDate:"2027-03-20",guestCount:200},model);
  assert.equal(cart.pricingMode,"FixedPrice"); assert.equal(cart.selections.length,2); assert.equal(cart.region,"Hà Nội");
  assert.throws(()=>selectService(cart,{serviceItemId:"third",categoryId:"photo",variantIds:[],addOnIds:[],hours:1},"shop"),/giá cố định/);
  assert.throws(()=>removeService(cart,"photo"),/giá cố định/);
});
test("old dynamic cart retains its replacement and removal behavior",()=>{
  const line={serviceItemId:"one",categoryId:"photo",variantIds:[],addOnIds:[],hours:1};
  const first=selectService(EMPTY_CART,line,"shop"),next=selectService(first,{...line,serviceItemId:"two"},"shop");
  assert.equal(next.selections.length,1);assert.equal(next.selections[0].serviceItemId,"two");assert.equal(removeService(next,"photo").selections.length,0);
});
test("fixed draft restores its mode without converting legacy drafts",()=>{
  const template=catalog().templates[0];const order={id:"draft",status:"Draft",region:"Hà Nội",items:template.components.map(c=>({...c,selectionJson:"{}"})),comboTemplateId:"fixed",guestCount:200};
  assert.equal(editableComboCart(order,template).pricingMode,"FixedPrice");
  assert.equal(editableComboCart(order,{...template,pricingMode:undefined}).pricingMode,undefined);
});
test("saved customer order prefers its immutable component pictures",()=>{
  const c=component("one",5000000);const order={id:"order",status:"Submitted",items:[{...c,title:"Frozen one",vendorName:"Shop",component:c}]};
  const model=comboCardModel(order,{media:{items:[{serviceItemId:"one",imageUrl:"https://example.invalid/changed.jpg",galleryUrls:[]}]},capabilities:{},kind:"combo"});
  assert.equal(model.photos[0],c.imageUrl);assert.equal(model.items[0].photos[0],c.imageUrl);
});
