﻿import { MouseEvent, MouseEventHandler } from "react";
import { Story, StoryFn } from "@storybook/react";

export function withPreventDefault(action: Function): MouseEventHandler<HTMLElement> {
    return (e: MouseEvent<HTMLElement>) => {
        e.preventDefault();
        action();
    };
}

export function databaseLocationComparator(lhs: databaseLocationSpecifier, rhs: databaseLocationSpecifier) {
    return lhs.nodeTag === rhs.nodeTag && lhs.shardNumber === rhs.shardNumber;
}

export function boundCopy<TArgs>(story: StoryFn<TArgs>): Story<TArgs> {
    return story.bind({});
}